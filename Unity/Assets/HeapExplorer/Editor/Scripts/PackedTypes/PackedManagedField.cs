//
// Heap Explorer for Unity. Copyright (c) 2019 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://bitbucket.org/pschraut/unityheapexplorer/
//
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

namespace HeapExplorer
{
    // Description of a field of a managed type.
    [Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    public struct PackedManagedField
    {
        // Offset of this field.
        public System.Int32 offset;

        // The typeindex into PackedMemorySnapshot.typeDescriptions of the type this field belongs to.
        public System.Int32 managedTypesArrayIndex;

        // Name of this field.
        public System.String name;

        // Is this field static?
        public System.Boolean isStatic;

        [NonSerialized] public bool isBackingField;

        const System.Int32 k_Version = 1;

        public static void Write(System.IO.BinaryWriter writer, PackedManagedField[] value)
        {
            writer.Write(k_Version);
            writer.Write(value.Length);

            for (int n = 0, nend = value.Length; n < nend; ++n)
            {
                writer.Write(value[n].name);
                writer.Write(value[n].offset);
                writer.Write(value[n].managedTypesArrayIndex);
                writer.Write(value[n].isStatic);
            }
        }

        public static void Read(System.IO.BinaryReader reader, out PackedManagedField[] value)
        {
            value = new PackedManagedField[0];

            var version = reader.ReadInt32();
            if (version >= 1)
            {
                var length = reader.ReadInt32();
                value = new PackedManagedField[length];

                for (int n = 0, nend = value.Length; n < nend; ++n)
                {
                    value[n].name = reader.ReadString();
                    value[n].offset = reader.ReadInt32();
                    value[n].managedTypesArrayIndex = reader.ReadInt32();
                    value[n].isStatic = reader.ReadBoolean();
                }
            }
        }

        public static PackedManagedField[] FromMemoryProfiler(UnityEditor.MemoryProfiler.FieldDescription[] source)
        {
            var value = new PackedManagedField[source.Length];
            for (int n = 0, nend = source.Length; n < nend; ++n)
            {
                value[n] = new PackedManagedField
                {
                    name = source[n].name,
                    offset = source[n].offset,
                    managedTypesArrayIndex = source[n].typeIndex,
                    isStatic = source[n].isStatic
                };
            };
            return value;
        }

        public override string ToString()
        {
            var text = string.Format("name: {0}, offset: {1}, typeIndex: {2}, isStatic: {3}", name, offset, managedTypesArrayIndex, isStatic);
            return text;
        }
    }
}