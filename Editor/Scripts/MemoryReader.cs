//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace HeapExplorer
{
    public class MemoryReader : AbstractMemoryReader
    {
        public MemoryReader(PackedMemorySnapshot snapshot)
            : base(snapshot)
        {
        }

        // returns the offset in the m_memorySection.bytes[] array of the specified address,
        // or -1 if the address cannot be read.
        protected override int TryBeginRead(System.UInt64 address)
        {
            // trying to access null?
            if (address == 0)
                return -1;

            // check if address still in the memory section we have already
            if (address >= m_StartAddress && address < m_EndAddress)
            {
                return (int)(address - m_StartAddress);
            }

            // it is a new section, try to find it
            var heapIndex = m_Snapshot.FindHeapOfAddress(address);
            if (heapIndex == -1)
            {
                Debug.LogWarningFormat("HeapExplorer: Heap at {0:X} not found. Haven't figured out why this happens yet. Perhaps related to .NET4 ScriptingRuntime?", address);
                return -1;
            }

            // setup new section
            var memorySection = m_Snapshot.managedHeapSections[heapIndex];
            m_StartAddress = memorySection.startAddress;
            m_EndAddress = m_StartAddress + (ulong)memorySection.bytes.LongLength;
            m_Bytes = memorySection.bytes;

            //Debug.LogFormat("accessing heap {0:X}", address);
            return (int)(address - m_StartAddress);
        }
    }


    public class StaticMemoryReader : AbstractMemoryReader
    {
        public StaticMemoryReader(PackedMemorySnapshot snapshot, System.Byte[] staticBytes)
            : base(snapshot)
        {
            m_Bytes = staticBytes;
        }

        // returns the offset in the m_memorySection.bytes[] array of the specified address,
        // or -1 if the address cannot be read.
        protected override int TryBeginRead(System.UInt64 address)
        {
            // trying to access null?
            if (m_Bytes == null || m_Bytes.LongLength == 0 || address >= (ulong)m_Bytes.LongLength)
                return -1;

            // check if address still in the memory section we have already
            if (address >= m_StartAddress && address < m_EndAddress)
            {
                return (int)(address);
            }

            // setup new section
            m_StartAddress = 0;
            m_EndAddress = m_StartAddress + (ulong)m_Bytes.LongLength;

            return (int)(address);
        }
    }

    abstract public class AbstractMemoryReader
    {
        protected PackedMemorySnapshot m_Snapshot;
        protected System.Byte[] m_Bytes;
        protected System.UInt64 m_StartAddress = System.UInt64.MaxValue;
        protected System.UInt64 m_EndAddress = System.UInt64.MaxValue;
        protected System.Int32 m_RecursionGuard;
        protected System.Text.StringBuilder m_StringBuilder = new System.Text.StringBuilder(128);
        protected System.Security.Cryptography.MD5 m_Hasher;

        protected AbstractMemoryReader(PackedMemorySnapshot snapshot)
        {
            m_Snapshot = snapshot;
        }

        protected abstract int TryBeginRead(System.UInt64 address);

        public System.SByte ReadSByte(System.UInt64 address)
        {
            var offset = TryBeginRead(address);
            if (offset < 0)
                return default(System.SByte);

            var value = (System.SByte)m_Bytes[offset];
            return value;
        }

        public System.Byte ReadByte(System.UInt64 address)
        {
            var offset = TryBeginRead(address);
            if (offset < 0)
                return default(System.Byte);

            var value = m_Bytes[offset];
            return value;
        }

        public System.Char ReadChar(System.UInt64 address)
        {
            var offset = TryBeginRead(address);
            if (offset < 0)
                return default(System.Char);

            var value = System.BitConverter.ToChar(m_Bytes, offset);
            return value;
        }

        public System.Boolean ReadBoolean(System.UInt64 address)
        {
            var offset = TryBeginRead(address);
            if (offset < 0)
                return default(System.Boolean);

            var value = System.BitConverter.ToBoolean(m_Bytes, offset);
            return value;
        }

        public System.Single ReadSingle(System.UInt64 address)
        {
            var offset = TryBeginRead(address);
            if (offset < 0)
                return default(System.Single);

            var value = System.BitConverter.ToSingle(m_Bytes, offset);
            return value;
        }

        public UnityEngine.Quaternion ReadQuaternion(System.UInt64 address)
        {
            var offset = TryBeginRead(address);
            if (offset < 0)
                return default(Quaternion);

            var sizeOfSingle = m_Snapshot.managedTypes[m_Snapshot.coreTypes.systemSingle].size;
            var value = new Quaternion()
            {
                x = ReadSingle(address + (uint)(sizeOfSingle * 0)),
                y = ReadSingle(address + (uint)(sizeOfSingle * 1)),
                z = ReadSingle(address + (uint)(sizeOfSingle * 2)),
                w = ReadSingle(address + (uint)(sizeOfSingle * 3))
            };

            return value;
        }

        public UnityEngine.Matrix4x4 ReadMatrix4x4(System.UInt64 address)
        {
            var offset = TryBeginRead(address);
            if (offset < 0)
                return default(Matrix4x4);

            var value = new Matrix4x4();

            var sizeOfSingle = m_Snapshot.managedTypes[m_Snapshot.coreTypes.systemSingle].size;
            var element = 0;
            for (var y = 0; y < 4; ++y)
            {
                for (var x = 0; x < 4; ++x)
                {
                    value[y, x] = ReadSingle(address + (uint)(sizeOfSingle * element));
                    element++;
                }
            }

            return value;
        }

        public System.Double ReadDouble(System.UInt64 address)
        {
            var offset = TryBeginRead(address);
            if (offset < 0)
                return default(System.Double);

            var value = System.BitConverter.ToDouble(m_Bytes, offset);
            return value;
        }

        public System.Decimal ReadDecimal(System.UInt64 address)
        {
            var offset = TryBeginRead(address);
            if (offset < 0)
                return default(System.Decimal);

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

            return new System.Decimal(lo, mid, hi, isNegative, (byte)scale);
        }

        public System.Int16 ReadInt16(System.UInt64 address)
        {
            var offset = TryBeginRead(address);
            if (offset < 0)
                return default(System.Int16);

            var value = System.BitConverter.ToInt16(m_Bytes, offset);
            return value;
        }

        public System.UInt16 ReadUInt16(System.UInt64 address)
        {
            var offset = TryBeginRead(address);
            if (offset < 0)
                return default(System.UInt16);

            var value = System.BitConverter.ToUInt16(m_Bytes, offset);
            return value;
        }

        public System.Int32 ReadInt32(System.UInt64 address)
        {
            var offset = TryBeginRead(address);
            if (offset < 0)
                return default(System.Int32);

            var value = System.BitConverter.ToInt32(m_Bytes, offset);
            return value;
        }

        public System.UInt32 ReadUInt32(System.UInt64 address)
        {
            var offset = TryBeginRead(address);
            if (offset < 0)
                return default(System.UInt32);

            var value = System.BitConverter.ToUInt32(m_Bytes, offset);
            return value;
        }

        public System.Int64 ReadInt64(System.UInt64 address)
        {
            var offset = TryBeginRead(address);
            if (offset < 0)
                return default(System.Int64);

            var value = System.BitConverter.ToInt64(m_Bytes, offset);
            return value;
        }

        public System.UInt64 ReadUInt64(System.UInt64 address)
        {
            var offset = TryBeginRead(address);
            if (offset < 0)
                return default(System.UInt64);

            var value = System.BitConverter.ToUInt64(m_Bytes, offset);
            return value;
        }

        public System.UInt64 ReadPointer(System.UInt64 address)
        {
            if (m_Snapshot.virtualMachineInformation.pointerSize == 8)
                return ReadUInt64(address);

            return ReadUInt32(address);
        }

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

        public System.String ReadString(System.UInt64 address)
        {
            // strings differ from any other data type in the CLR (other than arrays) in that their size isn’t fixed.
            // Normally the .NET GC knows the size of an object when it’s being allocated, because it’s based on the
            // size of the fields/properties within the object and they don’t change. However in .NET a string object
            // doesn’t contain a pointer to the actual string data, which is then stored elsewhere on the heap.
            // That raw data, the actual bytes that make up the text are contained within the string object itself.
            // http://mattwarren.org/2016/05/31/Strings-and-the-CLR-a-Special-Relationship/

            var offset = TryBeginRead(address);
            if (offset < 0)
                return default(System.String);

            // http://referencesource.microsoft.com/#mscorlib/system/string.cs
            var length = ReadInt32(address);
            if (length == 0)
                return "";

            if (length < 0)
                return "<error:length lesser 0>";

            const int kMaxStringLength = 1024 * 1024 * 10;
            if (length > kMaxStringLength)
                return string.Format("<error: length greater {0} bytes>", kMaxStringLength);

            offset += sizeof(System.Int32); // the length data aka sizeof(int)
            length *= sizeof(char); // length is specified in characters, but each char is 2 bytes wide

            // In one memory snapshot, it occured that a 1mb StringBuffer wasn't entirely available in m_bytes.
            // We check for such case here and fix the length.
            if ((m_Bytes.LongLength - offset) < length)
            {
                var wantedLength = length;
                length = m_Bytes.Length - offset;
                Debug.LogErrorFormat("Cannot read entire string 0x{0:X}. The wanted length in bytes is {1}, but the memory segment holds {2} bytes only.\n{3}...", address, wantedLength, length, System.Text.Encoding.Unicode.GetString(m_Bytes, offset, Mathf.Min(length, 32)));
            }


            var value = System.Text.Encoding.Unicode.GetString(m_Bytes, offset, length);
            return value;
        }

        // Gets the number of characters in the string at the specified address.
        public int ReadStringLength(System.UInt64 address)
        {
            var offset = TryBeginRead(address);
            if (offset < 0)
                return 0;

            var length = ReadInt32(address);
            if (length <= 0)
                return 0;

            return length;
        }

        /// <summary>
        /// Computes a checksum of the managed object specified at 'address'.
        /// This is used to find object duplicates.
        /// </summary>
        public UnityEngine.Hash128 ComputeObjectHash(System.UInt64 address, PackedManagedType type)
        {
            if (m_Hasher == null)
                m_Hasher = System.Security.Cryptography.MD5.Create();

            var content = ReadObjectBytes(address, type);
            if (content.Count == 0)
                return new Hash128();

            var bytes = m_Hasher.ComputeHash(content.Array, content.Offset, content.Count);
            if (bytes.Length != 16)
                return new Hash128();

            var v0 = (uint)(bytes[0] | bytes[1] << 8 | bytes[2] << 16 | bytes[3] << 24);
            var v1 = (uint)(bytes[4] << 32 | bytes[5] << 40 | bytes[6] << 48 | bytes[7] << 56);
            var v2 = (uint)(bytes[8] | bytes[9] << 8 | bytes[10] << 16 | bytes[11] << 24);
            var v3 = (uint)(bytes[12] << 32 | bytes[13] << 40 | bytes[14] << 48 | bytes[15] << 56);

            return new Hash128(v0, v1, v2, v3);
        }

        // address = object address (start of header)
        System.ArraySegment<byte> ReadObjectBytes(System.UInt64 address, PackedManagedType typeDescription)
        {
            var size = ReadObjectSize(address, typeDescription);
            if (size <= 0)
                return new System.ArraySegment<byte>();

            var offset = TryBeginRead(address);
            if (offset < 0)
                return new System.ArraySegment<byte>();

            // Unity bug? For a reason that I do not understand, sometimes a memory segment is smaller
            // than the actual size of an object. In order to workaround this issue, we make sure to never
            // try to read more data from the segment than is available.
            if ((m_Bytes.Length - offset) < size)
            {
                //var wantedLength = size;
                size = m_Bytes.Length - offset;
                //Debug.LogErrorFormat("Cannot read entire string 0x{0:X}. The requested length in bytes is {1}, but the memory segment holds {2} bytes only.\n{3}...", address, wantedLength, size, System.Text.Encoding.Unicode.GetString(m_bytes, offset, Mathf.Min(size, 32)));
                if (size <= 0)
                    return new System.ArraySegment<byte>();
            }

            var segment = new System.ArraySegment<byte>(m_Bytes, offset, size);
            return segment;
        }

        public int ReadObjectSize(System.UInt64 address, PackedManagedType typeDescription)
        {
            // System.Array
            // Do not display its pointer-size, but the actual size of its content.
            if (typeDescription.isArray)
            {
                if (typeDescription.baseOrElementTypeIndex < 0 || typeDescription.baseOrElementTypeIndex >= m_Snapshot.managedTypes.Length)
                {
                    var details = "";
                    details = "arrayRank=" + typeDescription.arrayRank + ", " +
                        "isArray=" + typeDescription.isArray + ", " +
                        "typeInfoAddress=" + typeDescription.typeInfoAddress.ToString("X") + ", " +
                        "address=" + address.ToString("X") + ", " +
                        "memoryreader=" + GetType().Name + ", " +
                        "isValueType=" + typeDescription.isValueType;

                    Debug.LogErrorFormat("ERROR: '{0}.baseOrElementTypeIndex' = {1} is out of range, ignoring. Details in second line\n{2}", typeDescription.name, typeDescription.baseOrElementTypeIndex, details);
                    return 1;
                }

                var arrayLength = ReadArrayLength(address, typeDescription);
                var elementType = m_Snapshot.managedTypes[typeDescription.baseOrElementTypeIndex];
                var elementSize = elementType.isValueType ? elementType.size : m_Snapshot.virtualMachineInformation.pointerSize;

                var size = m_Snapshot.virtualMachineInformation.arrayHeaderSize;
                size += elementSize * arrayLength;
                return size;
            }

            // System.String
            if (typeDescription.managedTypesArrayIndex == m_Snapshot.coreTypes.systemString)
            {
                var size = m_Snapshot.virtualMachineInformation.objectHeaderSize;
                size += sizeof(System.Int32); // string length
                size += ReadStringLength(address + (uint)m_Snapshot.virtualMachineInformation.objectHeaderSize) * sizeof(char);
                size += 2; // two null terminators aka \0\0
                return size;
            }

            return typeDescription.size;
        }

        public int ReadArrayLength(System.UInt64 address, PackedManagedType arrayType)
        {
            var vm = m_Snapshot.virtualMachineInformation;

            var bounds = ReadPointer(address + (ulong)vm.arrayBoundsOffsetInHeader);
            if (bounds == 0)
                return (int)ReadPointer(address + (ulong)vm.arraySizeOffsetInHeader);

            int length = 1;
            for (int i = 0; i != arrayType.arrayRank; i++)
            {
                var ptr = bounds + (ulong)(i * vm.pointerSize);
                length *= (int)ReadPointer(ptr);
            }
            return length;
        }

        public int ReadArrayLength(System.UInt64 address, PackedManagedType arrayType, int dimension)
        {
            if (dimension >= arrayType.arrayRank)
                return 0;

            var vm = m_Snapshot.virtualMachineInformation;

            var bounds = ReadPointer(address + (ulong)vm.arrayBoundsOffsetInHeader);
            if (bounds == 0)
                return (int)ReadPointer(address + (ulong)vm.arraySizeOffsetInHeader);

            var pointer = bounds + (ulong)(dimension * vm.pointerSize);
            var length = (int)ReadPointer(pointer);
            return length;
        }

        public string ReadFieldValueAsString(System.UInt64 address, PackedManagedType type)
        {
            ///////////////////////////////////////////////////////////////////
            // PRIMITIVE TYPES
            //
            // https://docs.microsoft.com/en-us/dotnet/standard/class-library-overview
            ///////////////////////////////////////////////////////////////////

            if (type.managedTypesArrayIndex == m_Snapshot.coreTypes.systemByte)
                return string.Format(StringFormat.Unsigned, ReadByte(address));

            if (type.managedTypesArrayIndex == m_Snapshot.coreTypes.systemSByte)
                return ReadByte(address).ToString();

            if (type.managedTypesArrayIndex == m_Snapshot.coreTypes.systemChar)
                return string.Format("'{0}'", ReadChar(address));

            if (type.managedTypesArrayIndex == m_Snapshot.coreTypes.systemBoolean)
                return ReadBoolean(address).ToString();

            if (type.managedTypesArrayIndex == m_Snapshot.coreTypes.systemSingle)
            {
                var v = ReadSingle(address);

                if (float.IsNaN(v)) return "float.NaN";
                if (v == float.MinValue) return string.Format("float.MinValue ({0:E})", v);
                if (v == float.MaxValue) return string.Format("float.MaxValue ({0:E})", v);
                if (float.IsPositiveInfinity(v)) return string.Format("float.PositiveInfinity ({0:E})", v);
                if (float.IsNegativeInfinity(v)) return string.Format("float.NegativeInfinity ({0:E})", v);
                if (v > 10000000 || v < -10000000) return v.ToString("E"); // If it's a big number, use scientified notation

                return v.ToString("F");
            }

            if (type.managedTypesArrayIndex == m_Snapshot.coreTypes.systemDouble)
            {
                var v = ReadDouble(address);

                if (double.IsNaN(v)) return "double.NaN";
                if (v == double.MinValue) return string.Format("double.MinValue ({0:E})", v);
                if (v == double.MaxValue) return string.Format("double.MaxValue ({0:E})", v);
                if (double.IsPositiveInfinity(v)) return string.Format("double.PositiveInfinity ({0:E})", v);
                if (double.IsNegativeInfinity(v)) return string.Format("double.NegativeInfinity ({0:E})", v);
                if (v > 10000000 || v < -10000000) return v.ToString("E"); // If it's a big number, use scientified notation

                return v.ToString("G");
            }

            if (type.managedTypesArrayIndex == m_Snapshot.coreTypes.systemInt16)
                return ReadInt16(address).ToString();

            if (type.managedTypesArrayIndex == m_Snapshot.coreTypes.systemUInt16)
                return string.Format(StringFormat.Unsigned, ReadUInt16(address));

            if (type.managedTypesArrayIndex == m_Snapshot.coreTypes.systemInt32)
                return ReadInt32(address).ToString();

            if (type.managedTypesArrayIndex == m_Snapshot.coreTypes.systemUInt32)
                return string.Format(StringFormat.Unsigned, ReadUInt32(address));

            if (type.managedTypesArrayIndex == m_Snapshot.coreTypes.systemInt64)
                return ReadInt64(address).ToString();

            if (type.managedTypesArrayIndex == m_Snapshot.coreTypes.systemUInt64)
                return string.Format(StringFormat.Unsigned, ReadUInt64(address));

            if (type.managedTypesArrayIndex == m_Snapshot.coreTypes.systemDecimal)
                return ReadDecimal(address).ToString();

            // String
            if (type.managedTypesArrayIndex == m_Snapshot.coreTypes.systemString)
            {
                // While string is actually a reference type, we handle it here, because it's so common.
                // Rather than showing the address, we show the value, which is the text.
                var pointer = ReadPointer(address);
                if (pointer == 0)
                    return "null";

                // TODO: HACK: Reading a static pointer, points actually into the HEAP. However,
                // since it's a StaticMemory reader in that case, we can't read the heap.
                // Therefore we create a new MemoryReader here.
                var heapreader = this;
                if (!(heapreader is MemoryReader))
                    heapreader = new MemoryReader(m_Snapshot);

                // https://stackoverflow.com/questions/3815227/understanding-clr-object-size-between-32-bit-vs-64-bit
                var value = '\"' + heapreader.ReadString(pointer + (ulong)m_Snapshot.virtualMachineInformation.objectHeaderSize) + '\"';
                return value;
            }

            ///////////////////////////////////////////////////////////////////
            // POINTER TYPES
            //
            // Simply display the address of it. A pointer type is either
            // a ReferenceType, or an IntPtr and UIntPtr.
            ///////////////////////////////////////////////////////////////////
            if (type.isPointer)
            {
                var pointer = ReadPointer(address);
                if (pointer == 0)
                    return "null";

                var value = string.Format(StringFormat.Address, pointer);
                return value;
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
                    return "{...}";

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

                        m_StringBuilder.Append(ReadFieldValueAsString(address + (ulong)offset, m_Snapshot.managedTypes[instanceFields[n].managedTypesArrayIndex]));
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
                return m_StringBuilder.ToString();
            }

            return "<???>";
        }
    }

    public static class StringFormat
    {
        public const string Address = "0x{0:X}";
        public const string Unsigned = "0x{0:X}";
    }
}
