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
    [Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    public struct PackedNativeUnityEngineObject
    {
        // The memory address of the native C++ object. This matches the "m_CachedPtr" field of UnityEngine.Object.
        public System.Int64 nativeObjectAddress;

        // InstanceId of this object.
        public System.Int32 instanceId;

        // Size in bytes of this object.
        public System.Int32 size;

        // The index used to obtain the native C++ type description from the PackedMemorySnapshot.nativeTypes array.
        public System.Int32 nativeTypesArrayIndex;

        // The index of this element in the PackedMemorySnapshot.nativeObjects array.
        [NonSerialized]
        public System.Int32 nativeObjectsArrayIndex;

        // The index of the C# counter-part in the PackedMemorySnapshot.managedObjects array or -1 if no C# object exists.
        [NonSerialized]
        public System.Int32 managedObjectsArrayIndex;

        // The hideFlags this native object has.
        public HideFlags hideFlags;

        // Name of this object.
        public System.String name;

        // Is this object persistent? (Assets are persistent, objects stored in scenes are persistent, dynamically created objects are not)
        public System.Boolean isPersistent;

        // Has this object has been marked as DontDestroyOnLoad?
        public System.Boolean isDontDestroyOnLoad;

        // Is this native object an internal Unity manager object?
        public System.Boolean isManager;
        
        const System.Int32 k_Version = 1;

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
                stateString = string.Format("Loading {0} Native Objects", length);
                value = new PackedNativeUnityEngineObject[length];

                for (int n = 0, nend = value.Length; n < nend; ++n)
                {
                    value[n].isPersistent = reader.ReadBoolean();
                    value[n].isDontDestroyOnLoad = reader.ReadBoolean();
                    value[n].isManager = reader.ReadBoolean();
                    value[n].name = reader.ReadString();
                    value[n].instanceId = reader.ReadInt32();
                    value[n].size = reader.ReadInt32();
                    value[n].nativeTypesArrayIndex = reader.ReadInt32();
                    value[n].hideFlags = (HideFlags)reader.ReadInt32();
                    value[n].nativeObjectAddress = reader.ReadInt64();

                    value[n].nativeObjectsArrayIndex = n;
                    value[n].managedObjectsArrayIndex = -1;
                }
            }
        }

        public static PackedNativeUnityEngineObject[] FromMemoryProfiler(UnityEditor.MemoryProfiler.PackedNativeUnityEngineObject[] source)
        {
            var value = new PackedNativeUnityEngineObject[source.Length];

            for (int n = 0, nend = source.Length; n < nend; ++n)
            {
                value[n] = new PackedNativeUnityEngineObject
                {
                    isPersistent = source[n].isPersistent,
                    isDontDestroyOnLoad = source[n].isDontDestroyOnLoad,
                    isManager = source[n].isManager,
                    name = source[n].name,
                    instanceId = source[n].instanceId,
                    size = source[n].size,
                    nativeTypesArrayIndex = source[n].nativeTypeArrayIndex,
                    hideFlags = source[n].hideFlags,
                    nativeObjectAddress = source[n].nativeObjectAddress,

                    nativeObjectsArrayIndex = n,
                    managedObjectsArrayIndex = -1,
                };
            }

            return value;
        }
    }
}
