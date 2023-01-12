//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//
using System;
using HeapExplorer.Utilities;

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

        public static void Read(System.IO.BinaryReader reader, out PackedManagedField[] value)
        {
            value = new PackedManagedField[0];

            var version = reader.ReadInt32();
            if (version >= 1)
            {
                var length = reader.ReadInt32();
                value = new PackedManagedField[length];

                for (int n = 0, nend = value.Length; n < nend; ++n) {
                    value[n] = Read(reader);
                }
            }
        }

        public static PackedManagedField Read(System.IO.BinaryReader reader) {
            var name = reader.ReadString();
            var offset = PInt.createOrThrow(reader.ReadInt32());
            var managedTypesArrayIndex = PInt.createOrThrow(reader.ReadInt32());
            var isStatic = reader.ReadBoolean();
            return new PackedManagedField(
                name: name,
                offset: offset,
                managedTypesArrayIndex: managedTypesArrayIndex,
                isStatic: isStatic
            );
        }

        public override string ToString()
        {
            var text = $"name: {name}, offset: {offset}, typeIndex: {managedTypesArrayIndex}, isStatic: {isStatic}";
            return text;
        }
    }
}
