//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//
using UnityEngine;
using System;
using HeapExplorer.Utilities;
using UnityEditor.Profiling.Memory.Experimental;

namespace HeapExplorer
{
    [Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    public struct PackedNativeUnityEngineObject
    {
        // The memory address of the native C++ object. This matches the "m_CachedPtr" field of UnityEngine.Object.
        public ulong nativeObjectAddress;

        // InstanceId of this object.
        public System.Int32 instanceId;

        // Size in bytes of this object.
        public ulong size;

        // The index used to obtain the native C++ type description from the PackedMemorySnapshot.nativeTypes array.
        public PInt nativeTypesArrayIndex;

        /// <summary>
        /// The index of this element in the <see cref="PackedMemorySnapshot.nativeObjects"/> array.
        /// </summary>
        [NonSerialized]
        public PInt nativeObjectsArrayIndex;

        /// <summary>
        /// The index of the C# counter-part in the <see cref="PackedMemorySnapshot.managedObjects"/> array or `None`
        /// if no C# object exists.
        /// </summary>
        [NonSerialized]
        public Option<PackedManagedObject.ArrayIndex> managedObjectsArrayIndex;

        // The hideFlags this native object has.
        public HideFlags hideFlags;

        /// <summary>
        /// Name of this object.
        /// </summary>
        public System.String name;

        // Is this object persistent? (Assets are persistent, objects stored in scenes are persistent, dynamically created objects are not)
        public System.Boolean isPersistent;

        // Has this object has been marked as DontDestroyOnLoad?
        public System.Boolean isDontDestroyOnLoad;

        // Is this native object an internal Unity manager object?
        public System.Boolean isManager;

        const System.Int32 k_Version = 2;

        public static void Write(System.IO.BinaryWriter writer, PackedNativeUnityEngineObject[] value)
        {
            writer.Write(k_Version);
            writer.Write(value.Length);

            for (int n = 0, nend = value.Length; n < nend; ++n)
            {
                writer.Write(value[n].isPersistent);
                writer.Write(value[n].isDontDestroyOnLoad);
                writer.Write(value[n].isManager);
                writer.Write(value[n].name);
                writer.Write(value[n].instanceId);
                writer.Write(value[n].size);
                writer.Write(value[n].nativeTypesArrayIndex);
                writer.Write((System.Int32)value[n].hideFlags);
                writer.Write(value[n].nativeObjectAddress);
            }
        }

        public static void Read(System.IO.BinaryReader reader, out PackedNativeUnityEngineObject[] value, out string stateString)
        {
            value = new PackedNativeUnityEngineObject[0];
            stateString = "";

            var version = reader.ReadInt32();
            if (version >= 1)
            {
                var length = reader.ReadInt32();
                stateString = $"Loading {length} Native Objects";
                value = new PackedNativeUnityEngineObject[length];

                for (int n = 0, nend = value.Length; n < nend; ++n)
                {
                    value[n].isPersistent = reader.ReadBoolean();
                    value[n].isDontDestroyOnLoad = reader.ReadBoolean();
                    value[n].isManager = reader.ReadBoolean();
                    value[n].name = reader.ReadString();
                    value[n].instanceId = reader.ReadInt32();
                    value[n].size = version >= 2 ? reader.ReadUInt64() : reader.ReadPInt();
                    value[n].nativeTypesArrayIndex = reader.ReadPInt();
                    value[n].hideFlags = (HideFlags)reader.ReadInt32();
                    value[n].nativeObjectAddress = version >= 2 ? reader.ReadUInt64() : reader.ReadInt64().ToULongClamped();
                    value[n].nativeObjectsArrayIndex = PInt.createOrThrow(n);
                }
            }
        }

        public static PackedNativeUnityEngineObject[] FromMemoryProfiler(UnityEditor.Profiling.Memory.Experimental.PackedMemorySnapshot snapshot)
        {
            var source = snapshot.nativeObjects;
            var value = new PackedNativeUnityEngineObject[source.GetNumEntries()];

            var sourceFlags = new ObjectFlags[source.flags.GetNumEntries()];
            source.flags.GetEntries(0, source.flags.GetNumEntries(), ref sourceFlags);

            var sourceObjectNames = new string[source.objectName.GetNumEntries()];
            source.objectName.GetEntries(0, source.objectName.GetNumEntries(), ref sourceObjectNames);

            var sourceInstanceIds = new int[source.instanceId.GetNumEntries()];
            source.instanceId.GetEntries(0, source.instanceId.GetNumEntries(), ref sourceInstanceIds);

            var sourceSizes = new ulong[source.size.GetNumEntries()];
            source.size.GetEntries(0, source.size.GetNumEntries(), ref sourceSizes);

            var sourceNativeTypeArrayIndex = new int[source.nativeTypeArrayIndex.GetNumEntries()];
            source.nativeTypeArrayIndex.GetEntries(0, source.nativeTypeArrayIndex.GetNumEntries(), ref sourceNativeTypeArrayIndex);

            var sourceHideFlags = new HideFlags[source.hideFlags.GetNumEntries()];
            source.hideFlags.GetEntries(0, source.hideFlags.GetNumEntries(), ref sourceHideFlags);

            var sourceNativeObjectAddress = new ulong[source.nativeObjectAddress.GetNumEntries()];
            source.nativeObjectAddress.GetEntries(0, source.nativeObjectAddress.GetNumEntries(), ref sourceNativeObjectAddress);

            for (int n = 0, nend = value.Length; n < nend; ++n) 
            {
                var nativeObjectAddress = sourceNativeObjectAddress[n];
                var nativeTypesArrayIndex = sourceNativeTypeArrayIndex[n];
                value[n] = new PackedNativeUnityEngineObject
                {
                    isPersistent = (sourceFlags[n] & ObjectFlags.IsPersistent) != 0,
                    isDontDestroyOnLoad = (sourceFlags[n] & ObjectFlags.IsDontDestroyOnLoad) != 0,
                    isManager = (sourceFlags[n] & ObjectFlags.IsManager) != 0,
                    name = sourceObjectNames[n],
                    instanceId = sourceInstanceIds[n],
                    size = sourceSizes[n],
                    nativeTypesArrayIndex = PInt.createOrThrow(nativeTypesArrayIndex),
                    hideFlags = sourceHideFlags[n],
                    nativeObjectAddress = nativeObjectAddress,

                    nativeObjectsArrayIndex = PInt.createOrThrow(n),
                };

                if (nativeTypesArrayIndex < 0) 
                {
                    Debug.LogWarningFormat(
                        "HeapExplorer: native object at {0:X} does not have an associated native type "
                        + "(nativeTypesArrayIndex={1}). This should not happen.",
                        nativeObjectAddress, nativeTypesArrayIndex
                    );
                }
            }

            return value;
        }
    }
}
