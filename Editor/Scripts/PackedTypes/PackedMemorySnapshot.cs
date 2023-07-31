//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//
using UnityEngine;
using System;
using System.Collections.Generic;
using HeapExplorer.Utilities;

namespace HeapExplorer
{
    [Serializable]
    public partial class PackedMemorySnapshot
    {
        public PackedMemorySnapshotHeader header = new PackedMemorySnapshotHeader();

        /// <summary>Descriptions of all the C++ unity types the profiled player knows about.</summary>
        public PackedNativeType[] nativeTypes = new PackedNativeType[0];

        /// <summary>All native C++ objects that were loaded at time of the snapshot.</summary>
        public PackedNativeUnityEngineObject[] nativeObjects = new PackedNativeUnityEngineObject[0];

        /// <summary>All GC handles in use in the memory snapshot.</summary>
        public PackedGCHandle[] gcHandles = new PackedGCHandle[0];

        /// <summary>
        /// The unmodified connections array of "from -> to" pairs that describe which things are keeping which other things alive.
        /// <para/>
        /// connections 0..gcHandles.Length-1 represent connections FROM gchandles
        /// <para/>
        /// connections gcHandles.Length..connections.Length-1 represent connections FROM native
        /// </summary>
        public PackedConnection[] connections = new PackedConnection[0];

        /// <summary>
        /// Array of actual managed heap memory sections. These are sorted by address after snapshot initialization.
        /// </summary>
        public PackedMemorySection[] managedHeapSections = new PackedMemorySection[0];

        /// <summary>
        /// Descriptions of all the managed types that were known to the virtual machine when the snapshot was taken.
        /// </summary>
        public PackedManagedType[] managedTypes = new PackedManagedType[0];

        /// <summary>
        /// Information about the virtual machine running executing the managed code inside the player.
        /// </summary>
        public PackedVirtualMachineInformation virtualMachineInformation;

        /// <summary>Type of <see cref="System.Single"/>.</summary>
        public PackedManagedType typeOfSingle => managedTypes[coreTypes.systemSingle];

        /// <summary>Type of <see cref="System.Byte"/>.</summary>
        public PackedManagedType typeOfByte => managedTypes[coreTypes.systemByte];

        /// <summary>
        /// Allows you to update the Unity progress bar with given <see cref="stepName"/>.
        /// </summary>
        public delegate void UpdateUnityUI(string stepName, int stepIndex, int totalSteps);
        
        /// <summary>
        /// Converts an Unity PackedMemorySnapshot to our own format.
        /// </summary>
        public static PackedMemorySnapshot FromMemoryProfiler(
            MemorySnapshotProcessingArgs args
        ) {
            var source = args.source;

            var value = new PackedMemorySnapshot();
            try {
                const int TOTAL_STEPS = 9;
                
                args.maybeUpdateUI?.Invoke("Verifying memory profiler snapshot", 0, TOTAL_STEPS);
                VerifyMemoryProfilerSnapshot(source);

                value.busyString = "Loading Header";
                args.maybeUpdateUI?.Invoke(value.busyString, 1, TOTAL_STEPS);
                value.header = PackedMemorySnapshotHeader.FromMemoryProfiler();

                value.busyString = $"Loading {source.nativeTypes.GetNumEntries()} Native Types";
                args.maybeUpdateUI?.Invoke(value.busyString, 2, TOTAL_STEPS);
                value.nativeTypes = PackedNativeType.FromMemoryProfiler(source);

                value.busyString = $"Loading {source.nativeObjects.GetNumEntries()} Native Objects";
                args.maybeUpdateUI?.Invoke(value.busyString, 3, TOTAL_STEPS);
                value.nativeObjects = PackedNativeUnityEngineObject.FromMemoryProfiler(source);

                value.busyString = $"Loading {source.gcHandles.GetNumEntries()} GC Handles";
                args.maybeUpdateUI?.Invoke(value.busyString, 4, TOTAL_STEPS);
                value.gcHandles = PackedGCHandle.FromMemoryProfiler(source);

                value.busyString = $"Loading {source.connections.GetNumEntries()} Object Connections";
                args.maybeUpdateUI?.Invoke(value.busyString, 5, TOTAL_STEPS);
                value.connections = PackedConnection.FromMemoryProfiler(source);

                value.busyString = $"Loading {source.managedHeapSections.GetNumEntries()} Managed Heap Sections";
                args.maybeUpdateUI?.Invoke(value.busyString, 6, TOTAL_STEPS);
                value.managedHeapSections = PackedMemorySection.FromMemoryProfiler(source);

                value.busyString = $"Loading {source.typeDescriptions.GetNumEntries()} Managed Types";
                args.maybeUpdateUI?.Invoke(value.busyString, 7, TOTAL_STEPS);
                value.managedTypes = PackedManagedType.FromMemoryProfiler(source);

                value.busyString = "Loading VM Information";
                args.maybeUpdateUI?.Invoke(value.busyString, 8, TOTAL_STEPS);
                value.virtualMachineInformation = PackedVirtualMachineInformation.FromMemoryProfiler(source);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
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

        /// <summary>
        /// Cache for <see cref="isOrContainsReferenceType"/>
        /// </summary>
        Dictionary<int, bool> _containsReferenceTypeCache = new Dictionary<int, bool>();

        /// <summary>
        /// Returns true if the type index for <see cref="managedTypes"/> is a reference type or contains a reference
        /// type as any of its fields. Value types are checked recursively, so this returns true for:
        /// 
        /// <code><![CDATA[
        /// struct Foo {
        ///   Bar bar;
        /// }
        ///
        /// struct Bar {
        ///   string str;
        /// }
        /// ]]></code> 
        /// </summary>
        /// TODO: test
        public bool isOrContainsReferenceType(int managedTypeIndex) =>
            _containsReferenceTypeCache.getOrUpdate(
                managedTypeIndex, 
                this, 
                (_managedTypeIndex, self) => {
                    var type = self.managedTypes[_managedTypeIndex];
                    if (type.isReferenceType)
                        return true;
                    
                    // Primitive types do not contain reference types in them, but contain self-references for structs,
                    // making us go into recursion forever.
                    if (type.isPrimitive)
                        return false;

                    var managedTypesLength = self.managedTypes.Length;
                    var instanceFields = type.instanceFields;

                    for (int n = 0, nend = instanceFields.Length; n < nend; ++n) {
                        var fieldTypeIndex = instanceFields[n].managedTypesArrayIndex;
                        if (fieldTypeIndex < 0 || fieldTypeIndex >= managedTypesLength) {
                            self.Error(
                                $"HeapExplorer: '{type.name}' field '{n}' is out of bounds '{fieldTypeIndex}', ignoring."
                            );
                            continue;
                        }

                        if (fieldTypeIndex == _managedTypeIndex) {
                            self.Error(
                                $"HeapExplorer: '{type.name}' field '{instanceFields[n].name}' is a value type that "
                                + "contains itself, that should be impossible!."
                            );
                            continue;
                        }
                        
                        if (self.isOrContainsReferenceType(fieldTypeIndex))
                            return true;
                    }

                    return false;
                }
            );
    }

    // Specifies how an Unity MemorySnapshot must be converted to HeapExplorer format.
    public class MemorySnapshotProcessingArgs {
        public readonly UnityEditor.Profiling.Memory.Experimental.PackedMemorySnapshot source;
        public readonly PackedMemorySnapshot.UpdateUnityUI maybeUpdateUI;

        public MemorySnapshotProcessingArgs(
            UnityEditor.Profiling.Memory.Experimental.PackedMemorySnapshot source, 
            PackedMemorySnapshot.UpdateUnityUI maybeUpdateUI = null
        ) {
            this.source = source;
            this.maybeUpdateUI = maybeUpdateUI;
        }
    }
}
