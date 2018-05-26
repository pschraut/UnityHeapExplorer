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
        public static PackedMemorySnapshot FromMemoryProfiler(UnityEditor.MemoryProfiler.PackedMemorySnapshot source)
        {
            var value = new PackedMemorySnapshot();

            value.header = PackedMemorySnapshotHeader.FromMemoryProfiler();

            value.stateString = "Loading Native Types";
            value.nativeTypes = PackedNativeType.FromMemoryProfiler(source.nativeTypes);

            value.stateString = "Loading Native Objects";
            value.nativeObjects = PackedNativeUnityEngineObject.FromMemoryProfiler(source.nativeObjects);

            value.stateString = "Loading GC Handles";
            value.gcHandles = PackedGCHandle.FromMemoryProfiler(source.gcHandles);

            value.stateString = "Loading Object Connections";
            value.connections = PackedConnection.FromMemoryProfiler(source.connections);

            value.stateString = "Loading Managed Heap Sections";
            value.managedHeapSections = PackedMemorySection.FromMemoryProfiler(source.managedHeapSections);

            value.stateString = "Loading Managed Types";
            value.managedTypes = PackedManagedType.FromMemoryProfiler(source.typeDescriptions);

            value.stateString = "Loading VM Information";
            value.virtualMachineInformation = PackedVirtualMachineInformation.FromMemoryProfiler(source.virtualMachineInformation);

            return value;
        }

        /// <summary>
        /// Loads a memory snapshot from the specified 'filePath' and stores the result in 'snapshot'.
        /// </summary>
        /// <param name="filePath">Absolute file path</param>
        public bool LoadFromFile(string filePath)
        {
            stateString = "Loading";

            using (var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Open))
            {
                using (var reader = new System.IO.BinaryReader(fileStream))
                {
                    stateString = "Loading Header";
                    PackedMemorySnapshotHeader.Read(reader, out header);
                    if (!header.isValid)
                        return false;

                    stateString = "Loading Native Types";
                    PackedNativeType.Read(reader, out nativeTypes);
                    if (nativeTypes == null || nativeTypes.Length == 0)
                    {
                        Debug.LogErrorFormat("'nativeTypes' array is empty.");
                        return false;
                    }

                    stateString = "Loading Native Objects";
                    PackedNativeUnityEngineObject.Read(reader, out nativeObjects);

                    stateString = "Loading GC Handles";
                    PackedGCHandle.Read(reader, out gcHandles);

                    stateString = "Loading Object Connections";
                    PackedConnection.Read(reader, out connections);

                    stateString = "Loading Managed Heap Sections";
                    PackedMemorySection.Read(reader, out managedHeapSections);
                    if (managedHeapSections == null || managedHeapSections.Length == 0)
                    {
                        Debug.LogErrorFormat("'managedHeapSections' array is empty.");
                        return false;
                    }

                    stateString = "Loading Managed Types";
                    PackedManagedType.Read(reader, out managedTypes);
                    if (managedTypes == null || managedTypes.Length == 0)
                    {
                        Debug.LogErrorFormat("'managedTypes' array is empty.");
                        return false;
                    }

                    stateString = "Loading VM Information";
                    PackedVirtualMachineInformation.Read(reader, out virtualMachineInformation);
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
}
