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
            try
            {
                value.stateString = "Loading Header";
                value.header = PackedMemorySnapshotHeader.FromMemoryProfiler();

                value.stateString = string.Format("Loading {0} Native Types", source.nativeTypes.Length);
                value.nativeTypes = PackedNativeType.FromMemoryProfiler(source.nativeTypes);

                value.stateString = string.Format("Loading {0} Native Objects", source.nativeObjects.Length);
                value.nativeObjects = PackedNativeUnityEngineObject.FromMemoryProfiler(source.nativeObjects);

                value.stateString = string.Format("Loading {0} GC Handles", source.gcHandles.Length);
                value.gcHandles = PackedGCHandle.FromMemoryProfiler(source.gcHandles);

                value.stateString = string.Format("Loading {0} Object Connections", source.connections.Length);
                value.connections = PackedConnection.FromMemoryProfiler(source.connections);

                value.stateString = string.Format("Loading {0} Managed Heap Sections", source.managedHeapSections.Length);
                value.managedHeapSections = PackedMemorySection.FromMemoryProfiler(source.managedHeapSections);

                value.stateString = string.Format("Loading {0} Managed Types", source.typeDescriptions.Length);
                value.managedTypes = PackedManagedType.FromMemoryProfiler(source.typeDescriptions);

                value.stateString = "Loading VM Information";
                value.virtualMachineInformation = PackedVirtualMachineInformation.FromMemoryProfiler(source.virtualMachineInformation);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                value = null;
            }
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
                    try
                    {
                        PackedMemorySnapshotHeader.Read(reader, out header, out stateString);
                        if (!header.isValid)
                            throw new Exception("Invalid header.");

                        PackedNativeType.Read(reader, out nativeTypes, out stateString);
                        if (nativeTypes == null || nativeTypes.Length == 0)
                            throw new Exception("snapshot.nativeTypes array mus not be empty.");

                        PackedNativeUnityEngineObject.Read(reader, out nativeObjects, out stateString);
                        PackedGCHandle.Read(reader, out gcHandles, out stateString);
                        PackedConnection.Read(reader, out connections, out stateString);

                        PackedMemorySection.Read(reader, out managedHeapSections, out stateString);
                        if (managedHeapSections == null || managedHeapSections.Length == 0)
                            throw new Exception("snapshot.managedHeapSections array mus not be empty.");    

                        PackedManagedType.Read(reader, out managedTypes, out stateString);
                        if (managedTypes == null || managedTypes.Length == 0)
                            throw new Exception("snapshot.managedTypes array mus not be empty.");

                        PackedVirtualMachineInformation.Read(reader, out virtualMachineInformation, out stateString);
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
}
