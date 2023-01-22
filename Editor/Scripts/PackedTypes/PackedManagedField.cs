//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//
using System;
using System.Collections.Generic;
using HeapExplorer.Utilities;
using static HeapExplorer.Utilities.Option;

namespace HeapExplorer
{
    /// <summary>
    /// Description of a field of a managed type.
    /// </summary>
    [Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    public struct PackedManagedField {
        /// <summary>
        /// Offset of this field.
        /// </summary>
        public readonly PInt offset;

        /// <summary>
        /// The type index into <see cref="PackedMemorySnapshot.managedTypes"/> of the type this field belongs to.
        /// </summary>
        public readonly PInt managedTypesArrayIndex;

        /// <summary>
        /// Name of this field.
        /// </summary>
        public string name;

        /// <summary>
        /// Is this field static?
        /// </summary>
        public readonly bool isStatic;

        [NonSerialized] public bool isBackingField;

        public PackedManagedField(PInt offset, PInt managedTypesArrayIndex, string name, bool isStatic) {
            this.offset = offset;
            this.managedTypesArrayIndex = managedTypesArrayIndex;
            this.name = name;
            this.isStatic = isStatic;
            isBackingField = false;
        }

        const System.Int32 k_Version = 1;

        public static void Write(System.IO.BinaryWriter writer, PackedManagedField[] value)
        {
            writer.Write(k_Version);
            writer.Write(value.Length);

            for (int n = 0, nend = value.Length; n < nend; ++n) {
                Write(writer, value[n]);
            }
        }

        public static void Write(System.IO.BinaryWriter writer, in PackedManagedField value) {
            writer.Write(value.name);
            writer.Write(value.offset.asInt);
            writer.Write(value.managedTypesArrayIndex.asInt);
            writer.Write(value.isStatic);
        }

        public static void Read(System.IO.BinaryReader reader, out PackedManagedField[] values)
        {
            var version = reader.ReadInt32();
            if (version >= 1)
            {
                var length = reader.ReadInt32();
                var list = new List<PackedManagedField>(capacity: length);

                for (var n = 0; n < length; ++n) {
                    if (Read(reader).valueOut(out var value)) list.Add(value);
                }

                values = list.ToArray();
            }
            else {
                throw new Exception($"Unknown {nameof(PackedManagedField)} version {version}.");
            }
        }

        /// <returns>`None` if it's incompatible data from an old format.</returns>
        public static Option<PackedManagedField> Read(System.IO.BinaryReader reader) {
            var name = reader.ReadString();
            var rawOffset = reader.ReadInt32();
            var managedTypesArrayIndex = PInt.createOrThrow(reader.ReadInt32());
            var isStatic = reader.ReadBoolean();
            if (isThreadStatic(isStatic, rawOffset)) return None._;
            else {
                var offset = PInt.createOrThrow(rawOffset);
                return Some(new PackedManagedField(
                    name: name,
                    offset: offset,
                    managedTypesArrayIndex: managedTypesArrayIndex,
                    isStatic: isStatic
                ));
            }
        }
        
        /// <summary>
        /// Offset will be -1 if the field is a static field with `[ThreadStatic]` attached to it.
        /// </summary>
        public static bool isThreadStatic(bool isStatic, int rawOffset) => isStatic && rawOffset == -1;

        public override string ToString()
        {
            var text = $"name: {name}, offset: {offset}, typeIndex: {managedTypesArrayIndex}, isStatic: {isStatic}";
            return text;
        }
    }
}
