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

                value.busyString = string.Format("Loading {0} Native Types", source.nativeTypes.GetNumEntries());
                value.nativeTypes = PackedNativeType.FromMemoryProfiler(source);

                value.busyString = string.Format("Loading {0} Native Objects", source.nativeObjects.GetNumEntries());
                value.nativeObjects = PackedNativeUnityEngineObject.FromMemoryProfiler(source);

                value.busyString = string.Format("Loading {0} GC Handles", source.gcHandles.GetNumEntries());
                value.gcHandles = PackedGCHandle.FromMemoryProfiler(source);

                value.busyString = string.Format("Loading {0} Object Connections", source.connections.GetNumEntries());
                value.connections = PackedConnection.FromMemoryProfiler(source);

                value.busyString = string.Format("Loading {0} Managed Heap Sections", source.managedHeapSections.GetNumEntries());
                value.managedHeapSections = PackedMemorySection.FromMemoryProfiler(source);

                value.busyString = string.Format("Loading {0} Managed Types", source.typeDescriptions.GetNumEntries());
                value.managedTypes = PackedManagedType.FromMemoryProfiler(source);

                value.busyString = "Loading VM Information";
                value.virtualMachineInformation = PackedVirtualMachineInformation.FromMemoryProfiler(source);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                value = null;
                throw;
            }
            return value;
        }

        static void VerifyMemoryProfilerSnapshot(UnityEditor.Profiling.Memory.Experimental.PackedMemorySnapshot snapshot)
        {
            if (snapshot == null)
                throw new Exception("No snapshot was found.");

            if (snapshot.typeDescriptions == null || snapshot.typeDescriptions.GetNumEntries() == 0)
                throw new Exception("The snapshot does not contain any 'typeDescriptions'. This is a known issue when using .NET 4.x Scripting Runtime.\n(Case 1079363) PackedMemorySnapshot: .NET 4.x Scripting Runtime breaks memory snapshot");

            if (snapshot.managedHeapSections == null || snapshot.managedHeapSections.GetNumEntries() == 0)
                throw new Exception("The snapshot does not contain any 'managedHeapSections'. This is a known issue when using .NET 4.x Scripting Runtime.\n(Case 1079363) PackedMemorySnapshot: .NET 4.x Scripting Runtime breaks memory snapshot");

            if (snapshot.gcHandles == null || snapshot.gcHandles.GetNumEntries() == 0)
                throw new Exception("The snapshot does not contain any 'gcHandles'. This is a known issue when using .NET 4.x Scripting Runtime.\n(Case 1079363) PackedMemorySnapshot: .NET 4.x Scripting Runtime breaks memory snapshot");

            if (snapshot.nativeTypes == null || snapshot.nativeTypes.GetNumEntries() == 0)
                throw new Exception("The snapshot does not contain any 'nativeTypes'.");

            if (snapshot.nativeObjects == null || snapshot.nativeObjects.GetNumEntries() == 0)
                throw new Exception("The snapshot does not contain any 'nativeObjects'.");

            if (snapshot.connections == null || snapshot.connections.GetNumEntries() == 0)
                throw new Exception("The snapshot does not contain any 'connections'.");
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
        public UnityEditor.Profiling.Memory.Experimental.PackedMemorySnapshot source;
    }
}
