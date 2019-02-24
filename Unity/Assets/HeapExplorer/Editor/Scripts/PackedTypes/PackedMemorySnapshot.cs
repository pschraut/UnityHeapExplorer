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
    public partial class PackedMemorySnapshot
    {
        public PackedMemorySnapshotHeader header = new PackedMemorySnapshotHeader();

        // Descriptions of all the C++ unity types the profiled player knows about.
        public PackedNativeType[] nativeTypes = new PackedNativeType[0];

        // All native C++ objects that were loaded at time of the snapshot.
        public PackedNativeUnityEngineObject[] nativeObjects = new PackedNativeUnityEngineObject[0];

        // All GC handles in use in the memorysnapshot.
        public PackedGCHandle[] gcHandles = new PackedGCHandle[0];

        // The unmodified connections array of "from -> to" pairs that describe which things are keeping which other things alive.
        // connections 0..gcHandles.Length-1 represent connections FROM gchandles
        // connections gcHandles.Length..connections.Length-1 represent connections FROM native
        public PackedConnection[] connections = new PackedConnection[0];

        // Array of actual managed heap memory sections. These are sorted by address after snapshot initialization.
        public PackedMemorySection[] managedHeapSections = new PackedMemorySection[0];

        // Descriptions of all the managed types that were known to the virtual machine when the snapshot was taken.
        public PackedManagedType[] managedTypes = new PackedManagedType[0];

        // Information about the virtual machine running executing the managade code inside the player.
        public PackedVirtualMachineInformation virtualMachineInformation = new PackedVirtualMachineInformation();

        /// <summary>
        /// Converts an Unity PackedMemorySnapshot to our own format.
        /// </summary>
        public static PackedMemorySnapshot FromMemoryProfiler(MemorySnapshotProcessingArgs args)
        {
            var source = args.source;

            var value = new PackedMemorySnapshot();
            try
            {
                VerifyMemoryProfilerSnapshot(source);

                value.busyString = "Loading Header";
                value.header = PackedMemorySnapshotHeader.FromMemoryProfiler();

                value.busyString = string.Format("Loading {0} Native Types", source.nativeTypes.Length);
                value.nativeTypes = PackedNativeType.FromMemoryProfiler(source.nativeTypes);

                value.busyString = string.Format("Loading {0} Native Objects", source.nativeObjects.Length);
                value.nativeObjects = PackedNativeUnityEngineObject.FromMemoryProfiler(source.nativeObjects);

                value.busyString = string.Format("Loading {0} GC Handles", source.gcHandles.Length);
                value.gcHandles = PackedGCHandle.FromMemoryProfiler(source.gcHandles);

                value.busyString = string.Format("Loading {0} Object Connections", source.connections.Length);
                if (args.excludeNativeFromConnections)
                    value.connections = ConnectionsFromMemoryProfilerWithoutNativeHACK(value, source);
                else
                    value.connections = PackedConnection.FromMemoryProfiler(source.connections);

                value.busyString = string.Format("Loading {0} Managed Heap Sections", source.managedHeapSections.Length);
                value.managedHeapSections = PackedMemorySection.FromMemoryProfiler(source.managedHeapSections);

                value.busyString = string.Format("Loading {0} Managed Types", source.typeDescriptions.Length);
                value.managedTypes = PackedManagedType.FromMemoryProfiler(source.typeDescriptions);

                value.busyString = "Loading VM Information";
                value.virtualMachineInformation = PackedVirtualMachineInformation.FromMemoryProfiler(source.virtualMachineInformation);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                value = null;
                throw;
            }
            return value;
        }

        static void VerifyMemoryProfilerSnapshot(UnityEditor.MemoryProfiler.PackedMemorySnapshot snapshot)
        {
            if (snapshot == null)
                throw new Exception("No snapshot was found.");

            if (snapshot.typeDescriptions == null || snapshot.typeDescriptions.Length == 0)
                throw new Exception("The snapshot does not contain any 'typeDescriptions'. This is a known issue when using .NET 4.x Scripting Runtime.\n(Case 1079363) PackedMemorySnapshot: .NET 4.x Scripting Runtime breaks memory snapshot");

            if (snapshot.managedHeapSections == null || snapshot.managedHeapSections.Length == 0)
                throw new Exception("The snapshot does not contain any 'managedHeapSections'. This is a known issue when using .NET 4.x Scripting Runtime.\n(Case 1079363) PackedMemorySnapshot: .NET 4.x Scripting Runtime breaks memory snapshot");

            if (snapshot.gcHandles == null || snapshot.gcHandles.Length == 0)
                throw new Exception("The snapshot does not contain any 'gcHandles'. This is a known issue when using .NET 4.x Scripting Runtime.\n(Case 1079363) PackedMemorySnapshot: .NET 4.x Scripting Runtime breaks memory snapshot");

            if (snapshot.nativeTypes == null || snapshot.nativeTypes.Length == 0)
                throw new Exception("The snapshot does not contain any 'nativeTypes'.");

            if (snapshot.nativeObjects == null || snapshot.nativeObjects.Length == 0)
                throw new Exception("The snapshot does not contain any 'nativeObjects'.");

            if (snapshot.connections == null || snapshot.connections.Length == 0)
                throw new Exception("The snapshot does not contain any 'connections'.");
        }

        // this method is a hack that excludes native object connections to workaround an unity bug.
        // https://forum.unity.com/threads/wip-heap-explorer-memory-profiler-debugger-and-analyzer-for-unity.527949/page-2#post-3615223
        static PackedConnection[] ConnectionsFromMemoryProfilerWithoutNativeHACK(PackedMemorySnapshot snapshot, UnityEditor.MemoryProfiler.PackedMemorySnapshot source)
        {
            var managedStart = 0;
            var managedEnd = managedStart + source.gcHandles.Length;
            var nativeStart = managedStart + managedEnd;
            var nativeEnd = nativeStart + source.nativeObjects.Length;
            var output = new List<PackedConnection>(1024 * 1024);

            for (int n = 0, nend = source.connections.Length; n < nend; ++n)
            {
                if ((n % (nend / 100)) == 0)
                {
                    var progress = ((n + 1.0f) / nend) * 100;
                    snapshot.busyString = string.Format("Analyzing GCHandle Connections\n{0}/{1}, {2:F0}% done", n + 1, source.connections.Length, progress);
                }

                var connection = new PackedConnection
                {
                    from = source.connections[n].from,
                    to = source.connections[n].to,
                };

                connection.fromKind = PackedConnection.Kind.GCHandle;
                if (connection.from >= nativeStart && connection.from < nativeEnd)
                {
                    connection.from -= nativeStart;
                    connection.fromKind = PackedConnection.Kind.Native;
                }

                connection.toKind = PackedConnection.Kind.GCHandle;
                if (connection.to >= nativeStart && connection.to < nativeEnd)
                {
                    connection.to -= nativeStart;
                    connection.toKind = PackedConnection.Kind.Native;
                }

                if (connection.fromKind != PackedConnection.Kind.Native)
                    output.Add(connection);
            }

            snapshot.header.nativeObjectFromConnectionsExcluded = true;
            Debug.LogWarning("HeapExplorer: Native object 'from' connections are excluded due workaround an Unity bug. Thus the object connections in Heap Explorer show fewer connections than actually exist.\nhttps://forum.unity.com/threads/wip-heap-explorer-memory-profiler-debugger-and-analyzer-for-unity.527949/page-2#post-3615223");
            return output.ToArray();
        }

        /// <summary>
        /// Loads a memory snapshot from the specified 'filePath' and stores the result in 'snapshot'.
        /// </summary>
        /// <param name="filePath">Absolute file path</param>
        public bool LoadFromFile(string filePath)
        {
            busyString = "Loading";

            using (var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Open))
            {
                using (var reader = new System.IO.BinaryReader(fileStream))
                {
                    try
                    {
                        PackedMemorySnapshotHeader.Read(reader, out header, out busyString);
                        if (!header.isValid)
                            throw new Exception("Invalid header.");

                        PackedNativeType.Read(reader, out nativeTypes, out busyString);
                        PackedNativeUnityEngineObject.Read(reader, out nativeObjects, out busyString);
                        PackedGCHandle.Read(reader, out gcHandles, out busyString);
                        PackedConnection.Read(reader, out connections, out busyString);
                        PackedMemorySection.Read(reader, out managedHeapSections, out busyString);
                        PackedManagedType.Read(reader, out managedTypes, out busyString);
                        PackedVirtualMachineInformation.Read(reader, out virtualMachineInformation, out busyString);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogException(e);
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Saves the specfified memory snapshot as a file, using the specified 'filePath'.
        /// </summary>
        public void SaveToFile(string filePath)
        {
            using (var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.OpenOrCreate))
            {
                using (var writer = new System.IO.BinaryWriter(fileStream))
                {
                    PackedMemorySnapshotHeader.Write(writer, header);
                    PackedNativeType.Write(writer, nativeTypes);
                    PackedNativeUnityEngineObject.Write(writer, nativeObjects);
                    PackedGCHandle.Write(writer, gcHandles);
                    PackedConnection.Write(writer, connections);
                    PackedMemorySection.Write(writer, managedHeapSections);
                    PackedManagedType.Write(writer, managedTypes);
                    PackedVirtualMachineInformation.Write(writer, virtualMachineInformation);
                }
            }
        }
    }

    // Specifies how an Unity MemorySnapshot must be converted to HeapExplorer format.
    public class MemorySnapshotProcessingArgs
    {
        public UnityEditor.MemoryProfiler.PackedMemorySnapshot source;
        public bool excludeNativeFromConnections;
    }
}
