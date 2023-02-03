//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//

using System.Collections.Concurrent;
using System.Globalization;
using HeapExplorer.Utilities;
using UnityEngine;
using static HeapExplorer.Utilities.Option;

namespace HeapExplorer
{
    public class MemoryReader : AbstractMemoryReader
    {
        public MemoryReader(PackedMemorySnapshot snapshot)
            : base(snapshot)
        {
        }

        /// <summary>
        /// Returns the offset in the >m_memorySection.bytes[] array of the specified address,
        /// or `None` if the address cannot be read.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        protected override Option<int> TryBeginRead(ulong address)
        {
            // trying to access null?
            if (address == 0) return Utils.zeroAddressAccessError<int>(nameof(address));

            // check if address still in the memory section we have already
            if (address >= m_StartAddress && address < m_EndAddress) {
                return Some((int)(address - m_StartAddress));
            }

            // it is a new section, try to find it
            if (!m_Snapshot.FindHeapOfAddress(address).valueOut(out var heapIndex)) {
                Debug.LogWarning(
                    $"HeapExplorer: Heap at address='{address:X}' not found. Haven't figured out why this happens yet. "
                    + "Perhaps related to .NET4 ScriptingRuntime?"
                );
                return None._;
            }

            // setup new section
            var memorySection = m_Snapshot.managedHeapSections[heapIndex];
            m_StartAddress = memorySection.startAddress;
            m_EndAddress = m_StartAddress + (ulong)memorySection.bytes.LongLength;
            m_Bytes = memorySection.bytes;

            //Debug.LogFormat("accessing heap {0:X}", address);
            return Some((int)(address - m_StartAddress));
        }
    }

    public class StaticMemoryReader : AbstractMemoryReader
    {
        public StaticMemoryReader(
            PackedMemorySnapshot snapshot, byte[] staticBytes
        ) : base(snapshot) {
            m_Bytes = staticBytes;
        }

        /// <summary>
        /// returns the offset in the m_memorySection.bytes[] array of the specified address,
        /// or `None` if the address cannot be read.
        /// </summary>
        protected override Option<int> TryBeginRead(ulong address)
        {
            // trying to access null?
            if (m_Bytes == null || m_Bytes.LongLength == 0 || address >= (ulong)m_Bytes.LongLength)
                return None._;

            // check if address still in the memory section we have already
            if (address >= m_StartAddress && address < m_EndAddress)
            {
                return Some((int)address);
            }

            // setup new section
            m_StartAddress = 0;
            m_EndAddress = m_StartAddress + (ulong)m_Bytes.LongLength;

            return Some((int)address);
        }
    }

    abstract public class AbstractMemoryReader
    {
        protected PackedMemorySnapshot m_Snapshot;
        protected byte[] m_Bytes;
        protected ulong m_StartAddress = ulong.MaxValue;
        protected ulong m_EndAddress = ulong.MaxValue;
        protected int m_RecursionGuard;
        protected System.Text.StringBuilder m_StringBuilder = new System.Text.StringBuilder(128);
        protected System.Security.Cryptography.MD5 m_Hasher;
        ConcurrentDictionary<string, Unit> reportedErrors => m_Snapshot.reportedErrors;

        protected AbstractMemoryReader(PackedMemorySnapshot snapshot)
        {
            m_Snapshot = snapshot;
        }

        protected abstract Option<int> TryBeginRead(ulong address);

        public Option<byte> ReadByte(ulong address) => 
            TryBeginRead(address).map(m_Bytes, (offset, bytes) => bytes[offset]);

        public Option<char> ReadChar(ulong address) =>
            TryBeginRead(address).map(m_Bytes, (offset, bytes) => System.BitConverter.ToChar(bytes, offset));

        public Option<bool> ReadBoolean(ulong address) =>
            TryBeginRead(address).map(m_Bytes, (offset, bytes) => System.BitConverter.ToBoolean(bytes, offset));

        public Option<float> ReadSingle(ulong address) =>
            TryBeginRead(address).map(m_Bytes, (offset, bytes) => System.BitConverter.ToSingle(bytes, offset));

        public Option<Quaternion> ReadQuaternion(ulong address) {
            var singleType = m_Snapshot.typeOfSingle;
            if (!singleType.size.valueOut(out var sizeOfSingle)) {
                Utils.reportInvalidSizeError(singleType, reportedErrors);
                return None._;
            }

            if (!ReadSingle(address + (uint) (sizeOfSingle * 0)).valueOut(out var x)) return None._;
            if (!ReadSingle(address + (uint) (sizeOfSingle * 1)).valueOut(out var y)) return None._;
            if (!ReadSingle(address + (uint) (sizeOfSingle * 2)).valueOut(out var z)) return None._;
            if (!ReadSingle(address + (uint) (sizeOfSingle * 3)).valueOut(out var w)) return None._;
            
            var value = new Quaternion { x = x, y = y, z = z, w = w };
            return Some(value);
        }

        public Option<Color> ReadColor(ulong address) {
            var singleType = m_Snapshot.typeOfSingle;
            if (!singleType.size.valueOut(out var sizeOfSingle)) {
                Utils.reportInvalidSizeError(singleType, reportedErrors);
                return None._;
            }

            if (!ReadSingle(address + (uint) (sizeOfSingle * 0)).valueOut(out var r)) return None._;
            if (!ReadSingle(address + (uint) (sizeOfSingle * 1)).valueOut(out var g)) return None._;
            if (!ReadSingle(address + (uint) (sizeOfSingle * 2)).valueOut(out var b)) return None._;
            if (!ReadSingle(address + (uint) (sizeOfSingle * 3)).valueOut(out var a)) return None._;
            
            var value = new Color(r, g, b, a);
            return Some(value);
        }

        public Option<Color32> ReadColor32(ulong address) {
            var byteType = m_Snapshot.typeOfSingle;
            if (!byteType.size.valueOut(out var sizeOfByte)) {
                Utils.reportInvalidSizeError(byteType, reportedErrors);
                return None._;
            }

            if (!ReadByte(address + (uint) (sizeOfByte * 0)).valueOut(out var r)) return None._;
            if (!ReadByte(address + (uint) (sizeOfByte * 1)).valueOut(out var g)) return None._;
            if (!ReadByte(address + (uint) (sizeOfByte * 2)).valueOut(out var b)) return None._;
            if (!ReadByte(address + (uint) (sizeOfByte * 3)).valueOut(out var a)) return None._;
            
            var value = new Color32(r, g, b, a);
            return Some(value);
        }

        public Option<Matrix4x4> ReadMatrix4x4(ulong address) {
            var singleType = m_Snapshot.typeOfSingle;
            if (!singleType.size.valueOut(out var sizeOfSingle)) {
                Utils.reportInvalidSizeError(singleType, reportedErrors);
                return None._;
            }
            
            var value = new Matrix4x4();

            var element = 0;
            for (var y = 0; y < 4; ++y)
            {
                for (var x = 0; x < 4; ++x)
                {
                    if (!ReadSingle(address + (uint)(sizeOfSingle * element)).valueOut(out var single))
                        return None._;
                    value[y, x] = single;
                    element++;
                }
            }

            return Some(value);
        }

        public Option<double> ReadDouble(ulong address) =>
            TryBeginRead(address).map(m_Bytes, (offset, bytes) => System.BitConverter.ToDouble(bytes, offset));

        public Option<decimal> ReadDecimal(ulong address) {
            if (!TryBeginRead(address).valueOut(out var offset)) return None._;

            // The lo, mid, hi, and flags fields contain the representation of the
            // Decimal value. The lo, mid, and hi fields contain the 96-bit integer
            // part of the Decimal. Bits 0-15 (the lower word) of the flags field are
            // unused and must be zero; bits 16-23 contain must contain a value between
            // 0 and 28, indicating the power of 10 to divide the 96-bit integer part
            // by to produce the Decimal value; bits 24-30 are unused and must be zero;
            // and finally bit 31 indicates the sign of the Decimal value, 0 meaning
            // positive and 1 meaning negative.
            //
            // struct Decimal
            // {
            //     private int flags;
            //     private int hi;
            //     private int lo;
            //     private int mid;
            // }
            //
            // https://referencesource.microsoft.com/#mscorlib/system/decimal.cs

            const int SignMask = unchecked((int)0x80000000);
            const int ScaleMask = 0x00FF0000;
            const int ScaleShift = 16;

            var flags = System.BitConverter.ToInt32(m_Bytes, offset + 0);
            var hi = System.BitConverter.ToInt32(m_Bytes, offset + 4);
            var lo = System.BitConverter.ToInt32(m_Bytes, offset + 8);
            var mid = System.BitConverter.ToInt32(m_Bytes, offset + 12);

            var isNegative = (flags & SignMask) != 0;
            var scale = (flags & ScaleMask) >> ScaleShift;

            return Some(new decimal(lo, mid, hi, isNegative, (byte)scale));
        }

        public Option<short> ReadInt16(ulong address) =>
            TryBeginRead(address).map(m_Bytes, (offset, bytes) => System.BitConverter.ToInt16(bytes, offset));

        public Option<ushort> ReadUInt16(ulong address) =>
            TryBeginRead(address).map(m_Bytes, (offset, bytes) => System.BitConverter.ToUInt16(bytes, offset));

        public Option<int> ReadInt32(ulong address) =>
            TryBeginRead(address).map(m_Bytes, (offset, bytes) => System.BitConverter.ToInt32(bytes, offset));

        public Option<uint> ReadUInt32(ulong address) =>
            TryBeginRead(address).map(m_Bytes, (offset, bytes) => System.BitConverter.ToUInt32(bytes, offset));

        public Option<long> ReadInt64(ulong address) =>
            TryBeginRead(address).map(m_Bytes, (offset, bytes) => System.BitConverter.ToInt64(bytes, offset));

        public Option<ulong> ReadUInt64(ulong address) =>
            TryBeginRead(address).map(m_Bytes, (offset, bytes) => System.BitConverter.ToUInt64(bytes, offset));

        public Option<ulong> ReadPointer(ulong address) => 
            m_Snapshot.virtualMachineInformation.pointerSize == PointerSize._64Bit 
                ? ReadUInt64(address) 
                : ReadUInt32(address).map(value => (ulong) value);

        //public Vector2 ReadVector2(System.UInt64 address)
        //{
        //    var offset = TryBeginRead(address);
        //    if (offset < 0)
        //        return default(Vector2);

        //    var value = new Vector2()
        //    {
        //        x = ReadSingle(address + 0),
        //        y = ReadSingle(address + 4),
        //    };

        //    return value;
        //}

        public Option<string> ReadString(ulong address)
        {
            // strings differ from any other data type in the CLR (other than arrays) in that their size isn�t fixed.
            // Normally the .NET GC knows the size of an object when it�s being allocated, because it�s based on the
            // size of the fields/properties within the object and they don�t change. However in .NET a string object
            // doesn�t contain a pointer to the actual string data, which is then stored elsewhere on the heap.
            // That raw data, the actual bytes that make up the text are contained within the string object itself.
            // http://mattwarren.org/2016/05/31/Strings-and-the-CLR-a-Special-Relationship/

            if (!TryBeginRead(address).valueOut(out var offset)) return None._;

            // http://referencesource.microsoft.com/#mscorlib/system/string.cs
            if (!ReadInt32(address).valueOut(out var length)) {
                Debug.LogError($"Can't determine length of a string at address {address:X}, offset={offset}");
                return None._;
            }
            
            if (length == 0) return Some("");

            if (length < 0) {
                Debug.LogError($"Length of a string at address {address:X}, offset={offset} is less than 0! length={length}");
                return None._;
            }

            const int kMaxStringLength = 1024 * 1024 * 10;
            if (length > kMaxStringLength) {
                Debug.LogError(
                    $"Length of a string at address {address:X}, offset={offset} is greater than {kMaxStringLength}! "
                    + $"length={length}"
                );
                return None._;
            }

            offset += sizeof(int); // the length data aka sizeof(int)
            length *= sizeof(char); // length is specified in characters, but each char is 2 bytes wide

            // In one memory snapshot, it occured that a 1mb StringBuffer wasn't entirely available in m_bytes.
            // We check for such case here and fix the length.
            if ((m_Bytes.LongLength - offset) < length)
            {
                var wantedLength = length;
                length = m_Bytes.Length - offset;
                Debug.LogErrorFormat(
                    "Cannot read entire string 0x{0:X}. The wanted length in bytes is {1}, but the memory segment "
                    + "holds {2} bytes only.\n{3}...", 
                    address, wantedLength, length, System.Text.Encoding.Unicode.GetString(m_Bytes, offset, Mathf.Min(length, 32))
                );
            }


            var value = System.Text.Encoding.Unicode.GetString(m_Bytes, offset, length);
            return Some(value);
        }

        /// <summary>
        /// Gets the number of characters in the string at the specified address.
        /// </summary>
        public Option<int> ReadStringLength(ulong address) => ReadInt32(address);

        /// <summary>
        /// Computes a checksum of the managed object specified at 'address'.
        /// This is used to find object duplicates.
        /// </summary>
        public Option<Hash128> ComputeObjectHash(ulong address, PackedManagedType type)
        {
            if (m_Hasher == null)
                m_Hasher = System.Security.Cryptography.MD5.Create();

            if (!ReadObjectBytes(address, type).valueOut(out var content)) return None._;
            if (content.Count == 0)
                return Some(new Hash128());

            var bytes = m_Hasher.ComputeHash(content.Array, content.Offset, content.Count);
            if (bytes.Length != 16) {
                Debug.LogError($"Expected hash for address {address:X} to be 16 bytes, but it was {bytes.Length} bytes!");
                return None._;
            }

            var v0 = (uint)(bytes[0] | bytes[1] << 8 | bytes[2] << 16 | bytes[3] << 24);
            var v1 = (uint)(bytes[4] << 32 | bytes[5] << 40 | bytes[6] << 48 | bytes[7] << 56);
            var v2 = (uint)(bytes[8] | bytes[9] << 8 | bytes[10] << 16 | bytes[11] << 24);
            var v3 = (uint)(bytes[12] << 32 | bytes[13] << 40 | bytes[14] << 48 | bytes[15] << 56);

            return Some(new Hash128(v0, v1, v2, v3));
        }

        // address = object address (start of header)
        Option<System.ArraySegment<byte>> ReadObjectBytes(ulong address, PackedManagedType typeDescription) {
            if (!ReadObjectSize(address, typeDescription).valueOut(out var sizeP)) return None._;
            var size = sizeP.asInt;
            if (size <= 0) {
                Debug.LogError($"Object size for object at address {address:X} is 0 or less! (size={size})");
                return None._;
            }

            if (!TryBeginRead(address).valueOut(out var offset)) return None._;
            if (offset < 0) {
                Debug.LogError($"Object offset for object at address {address:X} is negative! (offset={offset})");
                return None._;
            }

            // Unity bug? For a reason that I do not understand, sometimes a memory segment is smaller
            // than the actual size of an object. In order to workaround this issue, we make sure to never
            // try to read more data from the segment than is available.
            if (m_Bytes.Length - offset < size)
            {
                //var wantedLength = size;
                size = m_Bytes.Length - offset;
                //Debug.LogErrorFormat("Cannot read entire string 0x{0:X}. The requested length in bytes is {1}, but the memory segment holds {2} bytes only.\n{3}...", address, wantedLength, size, System.Text.Encoding.Unicode.GetString(m_bytes, offset, Mathf.Min(size, 32)));
                if (size <= 0) {
                    Debug.LogError($"Object size for object at address {address:X} is 0 or less! (size={size})");
                    return None._;
                }
            }

            var segment = new System.ArraySegment<byte>(m_Bytes, offset, size);
            return Some(segment);
        }

        public Option<PInt> ReadObjectSize(
            ulong address, PackedManagedType typeDescription
        ) {
            // System.Array
            // Do not display its pointer-size, but the actual size of its content.
            {if (typeDescription.arrayRank.valueOut(out var arrayRank)) {
                if (
                    !typeDescription.baseOrElementTypeIndex.valueOut(out var baseOrElementTypeIndex) 
                    || baseOrElementTypeIndex >= m_Snapshot.managedTypes.Length
                ) {
                    var details = "";
                    details = 
                        "arrayRank=" + arrayRank + ", " +
                        "typeInfoAddress=" + typeDescription.typeInfoAddress.ToString("X") + ", " +
                        "address=" + address.ToString("X") + ", " +
                        "memoryreader=" + GetType().Name + ", " +
                        "isValueType=" + typeDescription.isValueType;

                    Debug.LogErrorFormat("ERROR: '{0}.baseOrElementTypeIndex' = {1} is out of range, ignoring. Details in second line\n{2}", typeDescription.name, typeDescription.baseOrElementTypeIndex, details);
                    return Some(PInt._1);
                }

                if (!ReadArrayLength(address, arrayRank).valueOut(out var arrayLength)) return None._;
                var elementType = m_Snapshot.managedTypes[baseOrElementTypeIndex];
                int elementSize;
                if (elementType.isValueType) {
                    if (!elementType.size.valueOut(out var pElementSize)) {
                        Utils.reportInvalidSizeError(elementType, reportedErrors);
                        return None._;
                    }

                    elementSize = pElementSize;
                }
                else {
                    elementSize = m_Snapshot.virtualMachineInformation.pointerSize.sizeInBytes();
                }

                var size = m_Snapshot.virtualMachineInformation.arrayHeaderSize.asInt;
                size += elementSize * arrayLength;
                return Some(PInt.createOrThrow(size));
            }}

            // System.String
            if (typeDescription.managedTypesArrayIndex == m_Snapshot.coreTypes.systemString)
            {
                var size = m_Snapshot.virtualMachineInformation.objectHeaderSize.asInt;
                size += sizeof(int); // string length
                var maybeStringLength = ReadStringLength(address + m_Snapshot.virtualMachineInformation.objectHeaderSize);
                if (!maybeStringLength.valueOut(out var stringLength)) return None._;
                size += stringLength * sizeof(char);
                size += 2; // two null terminators aka \0\0
                return Some(PInt.createOrThrow(size));
            }

            if (typeDescription.size.isLeft) Utils.reportInvalidSizeError(typeDescription, reportedErrors);
            return typeDescription.size.rightOption;
        }

        public Option<int> ReadArrayLength(ulong address, PInt arrayRank)
        {
            var vm = m_Snapshot.virtualMachineInformation;

            if (!ReadPointer(address + vm.arrayBoundsOffsetInHeader).valueOut(out var bounds)) return None._;
            if (bounds == 0)
                return ReadPointer(address + vm.arraySizeOffsetInHeader).map(v => (int)v);

            int length = 1;
            for (int i = 0; i != arrayRank; i++)
            {
                var ptr = bounds + (ulong)(i * vm.pointerSize.sizeInBytes());
                if (!ReadPointer(ptr).valueOut(out var value)) return None._;
                length *= (int)value;
            }
            return Some(length);
        }

        public Option<int> ReadArrayLength(ulong address, PackedManagedType arrayType, PInt arrayRank, int dimension)
        {
            if (dimension >= arrayRank) {
                Debug.LogError(
                    $"Trying to read dimension {dimension} while the array rank is {arrayRank} for array at "
                    + $"address {address:X} of type '{arrayType.name}'. Returning `None`."
                );
                return None._;
            }

            var vm = m_Snapshot.virtualMachineInformation;

            if (!ReadPointer(address + vm.arrayBoundsOffsetInHeader).valueOut(out var bounds)) return None._;
            if (bounds == 0)
                return ReadPointer(address + vm.arraySizeOffsetInHeader).map(v => (int)v);

            var pointer = bounds + (ulong)(dimension * vm.pointerSize.sizeInBytes());
            return ReadPointer(pointer).map(v => (int)v);
        }

        public Option<string> ReadFieldValueAsString(ulong address, PackedManagedType type)
        {
            ///////////////////////////////////////////////////////////////////
            // PRIMITIVE TYPES
            //
            // https://docs.microsoft.com/en-us/dotnet/standard/class-library-overview
            ///////////////////////////////////////////////////////////////////

            if (type.managedTypesArrayIndex == m_Snapshot.coreTypes.systemByte)
                return Some(string.Format(StringFormat.Unsigned, ReadByte(address)));

            if (type.managedTypesArrayIndex == m_Snapshot.coreTypes.systemSByte)
                return ReadByte(address).map(v => v.ToString());

            if (type.managedTypesArrayIndex == m_Snapshot.coreTypes.systemChar)
                return ReadChar(address).map(c => $"'{c}'");

            if (type.managedTypesArrayIndex == m_Snapshot.coreTypes.systemBoolean)
                return ReadBoolean(address).map(v => v.ToString());

            if (type.managedTypesArrayIndex == m_Snapshot.coreTypes.systemSingle) {
                if (!ReadSingle(address).valueOut(out var v)) return None._;

                if (float.IsNaN(v)) return Some("float.NaN");
                if (v == float.MinValue) return Some($"float.MinValue ({v:E})");
                if (v == float.MaxValue) return Some($"float.MaxValue ({v:E})");
                if (float.IsPositiveInfinity(v)) return Some($"float.PositiveInfinity ({v:E})");
                if (float.IsNegativeInfinity(v)) return Some($"float.NegativeInfinity ({v:E})");
                if (v > 10000000 || v < -10000000) return Some(v.ToString("E")); // If it's a big number, use scientified notation

                return Some(v.ToString("F"));
            }

            if (type.managedTypesArrayIndex == m_Snapshot.coreTypes.systemDouble) {
                if (!ReadDouble(address).valueOut(out var v)) return None._;

                if (double.IsNaN(v)) return Some("double.NaN");
                if (v == double.MinValue) return Some($"double.MinValue ({v:E})");
                if (v == double.MaxValue) return Some($"double.MaxValue ({v:E})");
                if (double.IsPositiveInfinity(v)) return Some($"double.PositiveInfinity ({v:E})");
                if (double.IsNegativeInfinity(v)) return Some($"double.NegativeInfinity ({v:E})");
                if (v > 10000000 || v < -10000000) return Some(v.ToString("E")); // If it's a big number, use scientified notation

                return Some(v.ToString("G"));
            }

            if (type.managedTypesArrayIndex == m_Snapshot.coreTypes.systemInt16)
                return ReadInt16(address).map(v => v.ToString());

            if (type.managedTypesArrayIndex == m_Snapshot.coreTypes.systemUInt16)
                return ReadUInt16(address).map(v => string.Format(StringFormat.Unsigned, v));

            if (type.managedTypesArrayIndex == m_Snapshot.coreTypes.systemInt32)
                return ReadInt32(address).map(v => v.ToString());

            if (type.managedTypesArrayIndex == m_Snapshot.coreTypes.systemUInt32)
                return ReadUInt32(address).map(v => string.Format(StringFormat.Unsigned, v));

            if (type.managedTypesArrayIndex == m_Snapshot.coreTypes.systemInt64)
                return ReadInt64(address).map(v => v.ToString());

            if (type.managedTypesArrayIndex == m_Snapshot.coreTypes.systemUInt64)
                return ReadUInt64(address).map(v => string.Format(StringFormat.Unsigned, v));

            if (type.managedTypesArrayIndex == m_Snapshot.coreTypes.systemDecimal)
                return ReadDecimal(address).map(v => v.ToString(CultureInfo.InvariantCulture));

            // String
            if (type.managedTypesArrayIndex == m_Snapshot.coreTypes.systemString)
            {
                // While string is actually a reference type, we handle it here, because it's so common.
                // Rather than showing the address, we show the value, which is the text.
                if (!ReadPointer(address).valueOut(out var pointer)) return None._;
                if (pointer == 0) return Some("null");

                // TODO: HACK: Reading a static pointer, points actually into the HEAP. However,
                // since it's a StaticMemory reader in that case, we can't read the heap.
                // Therefore we create a new MemoryReader here.
                var heapReader = this;
                if (!(heapReader is MemoryReader))
                    heapReader = new MemoryReader(m_Snapshot);

                // https://stackoverflow.com/questions/3815227/understanding-clr-object-size-between-32-bit-vs-64-bit
                return heapReader.ReadString(pointer + m_Snapshot.virtualMachineInformation.objectHeaderSize)
                    .map(v => '\"' + v + '\"');
            }

            ///////////////////////////////////////////////////////////////////
            // POINTER TYPES
            //
            // Simply display the address of it. A pointer type is either
            // a ReferenceType, or an IntPtr and UIntPtr.
            ///////////////////////////////////////////////////////////////////
            if (type.isPointer) {
                if (!ReadPointer(address).valueOut(out var pointer)) return None._;
                if (pointer == 0) return Some("null");

                var value = string.Format(StringFormat.Address, pointer);
                return Some(value);
            }

            ///////////////////////////////////////////////////////////////////
            // VALUE TYPES
            //
            // If it's a value type, read a few fields and show them similar to Visual Studio.
            // Vector2(11,22) = (11.0, 22.0)
            ///////////////////////////////////////////////////////////////////
            if (type.isValueType)
            {
                if (m_RecursionGuard >= 1)
                    return Some("{...}");

                // For the actual value, or value preview, we are interested in instance fields only.
                var instanceFields = type.instanceFields;

                m_StringBuilder.Length = 0;
                m_StringBuilder.Append('(');

                // If the struct contains further structs, aka nested structs, we might run into an infinite recursion.
                // Therefore, lets gracefully handle this case by detecting whether we calling it recursively.
                m_RecursionGuard++;

                try
                {
                    // Read the first fields, this should cover many structs such as Vector3, Quaternion, etc
                    var count = instanceFields.Length > 8 ? 8 : instanceFields.Length;
                    for (var n = 0; n < count; ++n)
                    {
                        var offset = 0;
                        if (n > 0)
                            offset += instanceFields[n].offset - m_Snapshot.virtualMachineInformation.objectHeaderSize; // TODO: this is trial&error. make sure to understand it!

                        m_StringBuilder.Append(
                            ReadFieldValueAsString(
                                address + (ulong)offset, 
                                m_Snapshot.managedTypes[instanceFields[n].managedTypesArrayIndex]
                            ).getOrElse("<read error>")
                        );
                        if (n < count - 1)
                            m_StringBuilder.Append(", ");
                    }

                    if (instanceFields.Length > count)
                        m_StringBuilder.Append(", ...");
                }
                finally
                {
                    m_RecursionGuard--;
                }

                m_StringBuilder.Append(')');
                return Some(m_StringBuilder.ToString());
            }

            return Some("<???>");
        }
    }

    public static class StringFormat
    {
        public const string Address = "0x{0:X}";
        public const string Unsigned = "0x{0:X}";
    }
}
