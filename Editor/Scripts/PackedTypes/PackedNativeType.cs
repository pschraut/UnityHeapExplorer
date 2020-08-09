//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

namespace HeapExplorer
{
    [Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    public struct PackedNativeType
    {
        // The index used to obtain the native C++ base class description from the PackedMemorySnapshot.nativeTypes array.
        public System.Int32 nativeBaseTypeArrayIndex;

        [NonSerialized]
        public System.Int32 nativeTypeArrayIndex;

        [NonSerialized]
        public System.Int32 managedTypeArrayIndex;

        // Number of all objects of this type.
        [NonSerialized]
        public System.Int32 totalObjectCount;

        // The size of all objects of this type.
        [NonSerialized]
        public System.Int64 totalObjectSize;

        // Name of this C++ unity type.
        public System.String name;

        const System.Int32 k_Version = 1;

        public static readonly PackedNativeType invalid = new PackedNativeType()
        {
            name = "<invalid>",
            nativeBaseTypeArrayIndex = -1,
            nativeTypeArrayIndex = -1,
            managedTypeArrayIndex = -1
        };

        /// <summary>
        /// Writes a PackedNativeType array to the specified writer.
        /// </summary>
        public static void Write(System.IO.BinaryWriter writer, PackedNativeType[] value)
        {
            writer.Write(k_Version);
            writer.Write(value.Length);

            for (int n = 0, nend = value.Length; n < nend; ++n)
            {
                writer.Write(value[n].name);
                writer.Write(value[n].nativeBaseTypeArrayIndex);
            }
        }

        /// <summary>
        /// Reads a PackedNativeType array from the specified reader and stores the result in the specified value.
        /// </summary>
        public static void Read(System.IO.BinaryReader reader, out PackedNativeType[] value, out string stateString)
        {
            value = new PackedNativeType[0];
            stateString = "";

            var version = reader.ReadInt32();
            if (version >= 1)
            {
                var length = reader.ReadInt32();
                stateString = string.Format("Loading {0} Native Types", length);

                value = new PackedNativeType[length];

                for (int n = 0, nend = value.Length; n < nend; ++n)
                {
                    value[n].name = reader.ReadString();
                    value[n].nativeBaseTypeArrayIndex = reader.ReadInt32();
                    value[n].nativeTypeArrayIndex = n;
                    value[n].managedTypeArrayIndex = -1;
                }
            }
        }

        public static PackedNativeType[] FromMemoryProfiler(UnityEditor.Profiling.Memory.Experimental.PackedMemorySnapshot snapshot)
        {
            var source = snapshot.nativeTypes;
            var value = new PackedNativeType[source.GetNumEntries()];

            var sourceTypeName = new string[source.typeName.GetNumEntries()];
            source.typeName.GetEntries(0, source.typeName.GetNumEntries(), ref sourceTypeName);

            var sourceNativeBaseTypeArrayIndex = new int[source.nativeBaseTypeArrayIndex.GetNumEntries()];
            source.nativeBaseTypeArrayIndex.GetEntries(0, source.nativeBaseTypeArrayIndex.GetNumEntries(), ref sourceNativeBaseTypeArrayIndex);

            for (int n = 0, nend = value.Length; n < nend; ++n)
            {
                value[n] = new PackedNativeType
                {
                    name = sourceTypeName[n],
                    nativeBaseTypeArrayIndex = sourceNativeBaseTypeArrayIndex[n],
                    nativeTypeArrayIndex = n,
                    managedTypeArrayIndex = -1,
                };
            };

            return value;
        }


    }
}
