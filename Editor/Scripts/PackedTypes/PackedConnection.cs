//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeapExplorer
{
    // A pair of from and to indices describing what object keeps what other object alive.
    [Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    public struct PackedConnection
    {
        public enum Kind : byte //System.Byte
        {
            None = 0,
            GCHandle = 1,
            Native = 2,
            Managed = 3, // managed connections are NOT in the snapshot, we add them ourselfs.
            StaticField = 4, // static connections are NOT in the snapshot, we add them ourself.

            // Must not get greater than 11, otherwise ComputeConnectionKey() fails!
        }

        public System.Int32 from; // Index into a gcHandles, nativeObjects.
        public System.Int32 to; // Index into a gcHandles, nativeObjects.

        [NonSerialized] public Kind fromKind;
        [NonSerialized] public Kind toKind;

        const System.Int32 k_Version = 1;

        public static void Write(System.IO.BinaryWriter writer, PackedConnection[] value)
        {
            writer.Write(k_Version);
            writer.Write(value.Length);

            for (int n = 0, nend = value.Length; n < nend; ++n)
            {
                writer.Write(value[n].from);
                writer.Write(value[n].to);
            }
        }

        public static void Read(System.IO.BinaryReader reader, out PackedConnection[] value, out string stateString)
        {
            value = new PackedConnection[0];
            stateString = "";

            var version = reader.ReadInt32();
            if (version >= 1)
            {
                var length = reader.ReadInt32();
                //stateString = string.Format("Loading {0} Object Connections", length);
                value = new PackedConnection[length];
                if (length == 0)
                    return;

                var onePercent = Math.Max(1, value.Length / 100);
                for (int n = 0, nend = value.Length; n < nend; ++n)
                {
                    if ((n % onePercent) == 0)
                        stateString = string.Format("Loading Object Connections\n{0}/{1}, {2:F0}% done", n + 1, length, ((n + 1) / (float)length) * 100);

                    value[n].from = reader.ReadInt32();
                    value[n].to = reader.ReadInt32();
                }
            }
        }

        public static PackedConnection[] FromMemoryProfiler(UnityEditor.Profiling.Memory.Experimental.PackedMemorySnapshot snapshot)
        {
            var result = new List<PackedConnection>(1024*1024);

            // Get all NativeObject instanceIds
            var nativeObjects = snapshot.nativeObjects;
            var nativeObjectsInstanceIds = new int[nativeObjects.instanceId.GetNumEntries()];
            nativeObjects.instanceId.GetEntries(0, nativeObjects.instanceId.GetNumEntries(), ref nativeObjectsInstanceIds);
            
            // Create lookup table from instanceId to NativeObject arrayindex
            var instanceIdToNativeObjectIndex = new Dictionary<int, int>(nativeObjectsInstanceIds.Length);
            for (var n=0; n< nativeObjectsInstanceIds.Length; ++n)
                instanceIdToNativeObjectIndex.Add(nativeObjectsInstanceIds[n], n);

            // Connect NativeObject with its GCHandle
            var nativeObjectsGCHandleIndices = new int[nativeObjects.gcHandleIndex.GetNumEntries()];
            nativeObjects.gcHandleIndex.GetEntries(0, nativeObjects.gcHandleIndex.GetNumEntries(), ref nativeObjectsGCHandleIndices);

            var gcHandles = snapshot.gcHandles;
            var gcHandlesCount = gcHandles.GetNumEntries();

            for (var n=0; n< nativeObjectsGCHandleIndices.Length; ++n)
            {
                if (nativeObjectsGCHandleIndices[n] < 0 || nativeObjectsGCHandleIndices[n] >= gcHandlesCount)
                    continue; // I guess -1 means no connection to a gcHandle

                var packed = new PackedConnection();
                packed.from = n; // nativeObject index
                packed.fromKind = Kind.Native;
                packed.to = nativeObjectsGCHandleIndices[n]; // gcHandle index
                packed.toKind = Kind.GCHandle;

                result.Add(packed);
            }
            Debug.LogFormat("found {0} connections, gcHandlesCount={1}", result.Count, gcHandlesCount);

            // Connect NativeObject with NativeObject

            var source = snapshot.connections;
            int[] sourceFrom = new int[source.from.GetNumEntries()];
            source.from.GetEntries(0, source.from.GetNumEntries(), ref sourceFrom);

            int[] sourceTo = new int[source.to.GetNumEntries()];
            source.to.GetEntries(0, source.to.GetNumEntries(), ref sourceTo);

            for (int n = 0, nend = sourceFrom.Length; n < nend; ++n)
            {
                var packed = new PackedConnection();

                if (!instanceIdToNativeObjectIndex.TryGetValue(sourceFrom[n], out packed.from))
                    continue; // NativeObject InstanceID not found

                if (!instanceIdToNativeObjectIndex.TryGetValue(sourceTo[n], out packed.to))
                    continue; // NativeObject InstanceID not found

                packed.fromKind = Kind.Native;
                packed.toKind = Kind.Native;

                result.Add(packed);
            }

            return result.ToArray();
        }
    }
}
