//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//
using System;
using HeapExplorer.Utilities;
using static HeapExplorer.Utilities.Option;

namespace HeapExplorer
{
    [Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    public struct PackedNativeType : PackedMemorySnapshot.TypeForSubclassSearch
    {
        /// <summary>
        /// The index used to obtain the native C++ base class description from the
        /// <see cref="PackedMemorySnapshot.nativeTypes"/> array or `None` if this has no base type.
        /// </summary>
        public Option<PInt> nativeBaseTypeArrayIndex;

        [NonSerialized]
        public PInt nativeTypeArrayIndex;

        [NonSerialized]
        public Option<PInt> managedTypeArrayIndex;

        // Number of all objects of this type.
        [NonSerialized]
        public PInt totalObjectCount;

        // The size of all objects of this type.
        [NonSerialized]
        public ulong totalObjectSize;

        // Name of this C++ unity type.
        public string name;

        /// <inheritdoc/>
        string PackedMemorySnapshot.TypeForSubclassSearch.name => name;

        /// <inheritdoc/>
        PInt PackedMemorySnapshot.TypeForSubclassSearch.typeArrayIndex => nativeTypeArrayIndex;

        /// <inheritdoc/>
        Option<PInt> PackedMemorySnapshot.TypeForSubclassSearch.baseTypeArrayIndex => nativeBaseTypeArrayIndex;

        const System.Int32 k_Version = 1;

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
                writer.Write(value[n].nativeBaseTypeArrayIndex.fold(-1, _ => _));
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
                stateString = $"Loading {length} Native Types";

                value = new PackedNativeType[length];

                for (int n = 0, nend = value.Length; n < nend; ++n)
                {
                    value[n].name = reader.ReadString();
                    var nativeBaseTypeArrayIndex = reader.ReadInt32();
                    value[n].nativeBaseTypeArrayIndex = 
                        nativeBaseTypeArrayIndex == -1 ? None._ : Some(PInt.createOrThrow(nativeBaseTypeArrayIndex));
                    value[n].nativeTypeArrayIndex = PInt.createOrThrow(n);
                    value[n].managedTypeArrayIndex = None._;
                }
            }
        }

        public static PackedNativeType[] FromMemoryProfiler(
            UnityEditor.Profiling.Memory.Experimental.PackedMemorySnapshot snapshot
        ) {
            var source = snapshot.nativeTypes;
            var value = new PackedNativeType[source.GetNumEntries()];

            var sourceTypeName = new string[source.typeName.GetNumEntries()];
            source.typeName.GetEntries(0, source.typeName.GetNumEntries(), ref sourceTypeName);

            var sourceNativeBaseTypeArrayIndex = new int[source.nativeBaseTypeArrayIndex.GetNumEntries()];
            source.nativeBaseTypeArrayIndex.GetEntries(
                0, source.nativeBaseTypeArrayIndex.GetNumEntries(), ref sourceNativeBaseTypeArrayIndex
            );

            for (int n = 0, nend = value.Length; n < nend; ++n) {
                var nativeBaseTypeArrayIndex = sourceNativeBaseTypeArrayIndex[n];
                value[n] = new PackedNativeType
                {
                    name = sourceTypeName[n],
                    nativeBaseTypeArrayIndex = 
                        nativeBaseTypeArrayIndex == -1 ? None._ : Some(PInt.createOrThrow(nativeBaseTypeArrayIndex)),
                    nativeTypeArrayIndex = PInt.createOrThrow(n),
                    managedTypeArrayIndex = None._,
                };
            }

            return value;
        }


    }
}
