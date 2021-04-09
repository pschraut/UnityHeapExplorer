//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//

//#define ENABLE_PROFILING
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.Threading;

namespace HeapExplorer
{
    public partial class PackedMemorySnapshot
    {
        /// <summary>
        /// An array of 4096bytes aligned memory sections. These appear to me the actual managed memory sections.
        /// non-aligned sections seem to be internal / MonoMemPool sections.
        /// </summary>
        /// <see cref="https://forum.unity.com/threads/wip-heap-explorer-memory-profiler-debugger-and-analyzer-for-unity.527949/page-3#post-3902371"/>
        public PackedMemorySection[] alignedManagedHeapSections = new PackedMemorySection[0];

        /// <summary>
        /// An array of extracted managed objects from the managed heap memory.
        /// </summary>
        [NonSerialized]
        public PackedManagedObject[] managedObjects = new PackedManagedObject[0];

        /// <summary>
        /// An array of managed static fields.
        /// </summary>
        [NonSerialized]
        public PackedManagedStaticField[] managedStaticFields = new PackedManagedStaticField[0];

        /// <summary>
        /// Indices into the managedTypes array of static types.
        /// </summary>
        [NonSerialized]
        public int[] managedStaticTypes = new int[0];

        /// <summary>
        /// Indices into the connections array.
        /// </summary>
        [NonSerialized]
        public int[] connectionsToMonoScripts = new int[0];

        /// <summary>
        /// CoreTypes is a helper class that contains indices to frequently used classes, such as MonoBehaviour.
        /// </summary>
        [NonSerialized]
        public PackedCoreTypes coreTypes = new PackedCoreTypes();

        /// <summary>
        /// Write to busyString while processing, such as loading a memory snapshot, causes heap explorer
        /// to display the busyString in the main window.
        /// </summary>
        [NonSerialized]
        public string busyString = "";

        /// <summary>
        /// Gets whether the snapshot is currently busy, such as loading.
        /// </summary>
        public bool isBusy
        {
            get;
            private set;
        }

        public bool isProcessing
        {
            get;
            private set;
        }

        public bool abortActiveStepRequested
        {
            get;
            set;
        }

        [NonSerialized] Dictionary<UInt64, int> m_FindManagedObjectOfNativeObjectLUT;
        [NonSerialized] Dictionary<UInt64, int> m_FindManagedTypeOfTypeInfoAddressLUT;
        [NonSerialized] Dictionary<UInt64, int> m_FindNativeObjectOfAddressLUT;
        [NonSerialized] Dictionary<UInt64, int> m_FindManagedObjectOfAddressLUT;
        [NonSerialized] Dictionary<ulong, int> m_FindGCHandleOfTargetAddressLUT;
        [NonSerialized] Dictionary<UInt64, List<PackedConnection>> m_ConnectionsFrom = new Dictionary<ulong, List<PackedConnection>>(1024 * 32);
        [NonSerialized] Dictionary<UInt64, List<PackedConnection>> m_ConnectionsTo = new Dictionary<ulong, List<PackedConnection>>(1024 * 32);

        public PackedMemorySnapshot()
        {
            isBusy = true;
        }

        public void Error(string format, params object[] args)
        {
            var text = string.Format(format, args);
            //errors.Add(text);
            Debug.LogError(text);
        }

        public void Warning(string format, params object[] args)
        {
            var text = string.Format(format, args);
            //errors.Add(text);
            Debug.LogWarning(text);
        }

        public void FindManagedStaticFieldsOfType(PackedManagedType type, List<int> target)
        {
            if (target == null)
                return;

            for (int n = 0, nend = managedStaticFields.Length; n < nend; ++n)
            {
                if (managedStaticFields[n].managedTypesArrayIndex == type.managedTypesArrayIndex)
                    target.Add(managedStaticFields[n].staticFieldsArrayIndex);
            }
        }

        /// <summary>
        /// Find the managed object type at the specified address.
        /// </summary>
        /// <param name="address">The managed object memory address.</param>
        /// <returns>An index into the snapshot.managedTypes array on success, -1 otherwise.</returns>
        public int FindManagedObjectTypeOfAddress(System.UInt64 address)
        {
            // IL2CPP has the class pointer as the first member of the object.
            int typeIndex = FindManagedTypeOfTypeInfoAddress(address);
            if (typeIndex != -1)
                return typeIndex;

            // Mono has a vtable pointer as the first member of the object.
            // The first member of the vtable is the class pointer.
            var heapIndex = FindHeapOfAddress(address);
            if (heapIndex == -1)
                return -1;

            var vtable = managedHeapSections[heapIndex];
            var offset = (int)(address - vtable.startAddress);
            var vtableClassPointer = virtualMachineInformation.pointerSize == 8 ? BitConverter.ToUInt64(vtable.bytes, offset) : BitConverter.ToUInt32(vtable.bytes, offset);
            if (vtableClassPointer == 0)
                return -1;

            // Mono has a vtable pointer as the first member of the object.
            // The first member of the vtable is the class pointer.
            heapIndex = FindHeapOfAddress(vtableClassPointer);
            if (heapIndex == -1)
            {
                heapIndex = FindManagedTypeOfTypeInfoAddress(vtableClassPointer);
                //Error("Cannot find memory segment for vtableClassPointer pointing at address '{0:X}'.", vtableClassPointer);
                return heapIndex;
            }

            offset = (int)(vtableClassPointer - managedHeapSections[heapIndex].startAddress);

            if (virtualMachineInformation.pointerSize == 8)
                typeIndex = FindManagedTypeOfTypeInfoAddress(BitConverter.ToUInt64(managedHeapSections[heapIndex].bytes, offset));
            else if (virtualMachineInformation.pointerSize == 4)
                typeIndex = FindManagedTypeOfTypeInfoAddress(BitConverter.ToUInt32(managedHeapSections[heapIndex].bytes, offset));

            return typeIndex;
        }

        /// <summary>
        /// Find the managed object counter-part of a native object.
        /// </summary>
        /// <param name="nativeObjectAddress">The native object address.</param>
        /// <returns>An index into the snapshot.managedObjects array on success, -1 otherwise.</returns>
        public int FindManagedObjectOfNativeObject(UInt64 nativeObjectAddress)
        {
            if (nativeObjectAddress == 0)
                return -1;

            if (m_FindManagedObjectOfNativeObjectLUT == null)
            {
                m_FindManagedObjectOfNativeObjectLUT = new Dictionary<ulong, int>(managedObjects.Length);
                for (int n = 0, nend = managedObjects.Length; n < nend; ++n)
                {
                    if (managedObjects[n].nativeObjectsArrayIndex >= 0)
                    {
                        var address = (ulong)nativeObjects[managedObjects[n].nativeObjectsArrayIndex].nativeObjectAddress;
                        m_FindManagedObjectOfNativeObjectLUT[address] = n;
                    }
                }
            }

            int index;
            if (m_FindManagedObjectOfNativeObjectLUT.TryGetValue(nativeObjectAddress, out index))
                return index;

            return -1;
        }

        /// <summary>
        /// Find the managed object at the specified address.
        /// </summary>
        /// <param name="managedObjectAddress">The managed object address.</param>
        /// <returns>An index into the snapshot.managedObjects array on success, -1 otherwise.</returns>
        public int FindManagedObjectOfAddress(UInt64 managedObjectAddress)
        {
            if (managedObjectAddress == 0)
                return -1;

            if (m_FindManagedObjectOfAddressLUT == null)
            {
                m_FindManagedObjectOfAddressLUT = new Dictionary<ulong, int>(managedObjects.Length);
                for (int n = 0, nend = managedObjects.Length; n < nend; ++n)
                    m_FindManagedObjectOfAddressLUT[managedObjects[n].address] = n;
            }

            int index;
            if (m_FindManagedObjectOfAddressLUT.TryGetValue(managedObjectAddress, out index))
                return index;

            return -1;
        }

        /// <summary>
        /// Find the native object at the specified address.
        /// </summary>
        /// <param name="nativeObjectAddress">The native object address.</param>
        /// <returns>An index into the snapshot.nativeObjects array on success, -1 otherwise.</returns>
        public int FindNativeObjectOfAddress(UInt64 nativeObjectAddress)
        {
            if (nativeObjectAddress == 0)
                return -1;

            if (m_FindNativeObjectOfAddressLUT == null)
            {
                m_FindNativeObjectOfAddressLUT = new Dictionary<ulong, int>(nativeObjects.Length);
                for (int n = 0, nend = nativeObjects.Length; n < nend; ++n)
                    m_FindNativeObjectOfAddressLUT[(ulong)nativeObjects[n].nativeObjectAddress] = n;
            }

            int index;
            if (m_FindNativeObjectOfAddressLUT.TryGetValue(nativeObjectAddress, out index))
                return index;

            return -1;
        }

        /// <summary>
        /// Find the GCHandle at the specified address.
        /// </summary>
        /// <param name="targetAddress">The corresponding managed object address.</param>
        /// <returns>An index into the snapshot.gcHandles array on success, -1 otherwise.</returns>
        public int FindGCHandleOfTargetAddress(UInt64 targetAddress)
        {
            if (targetAddress == 0)
                return -1;

            if (m_FindGCHandleOfTargetAddressLUT == null)
            {
                m_FindGCHandleOfTargetAddressLUT = new Dictionary<ulong, int>(gcHandles.Length);
                for (int n = 0, nend = gcHandles.Length; n < nend; ++n)
                    m_FindGCHandleOfTargetAddressLUT[gcHandles[n].target] = gcHandles[n].gcHandlesArrayIndex;
            }

            int index;
            if (m_FindGCHandleOfTargetAddressLUT.TryGetValue(targetAddress, out index))
                return index;

            return -1;
        }

        /// <summary>
        /// Find the managed type of the address where the TypeInfo is stored.
        /// </summary>
        /// <param name="typeInfoAddress">The type info address.</param>
        /// <returns>An index into the snapshot.managedTypes array on success, -1 otherwise.</returns>
        public int FindManagedTypeOfTypeInfoAddress(UInt64 typeInfoAddress)
        {
            if (typeInfoAddress == 0)
                return -1;

            if (m_FindManagedTypeOfTypeInfoAddressLUT == null)
            {
                m_FindManagedTypeOfTypeInfoAddressLUT = new Dictionary<ulong, int>(managedTypes.Length);
                for (int n = 0, nend = managedTypes.Length; n < nend; ++n)
                    m_FindManagedTypeOfTypeInfoAddressLUT[managedTypes[n].typeInfoAddress] = managedTypes[n].managedTypesArrayIndex;
            }

            int index;
            if (m_FindManagedTypeOfTypeInfoAddressLUT.TryGetValue(typeInfoAddress, out index))
                return index;

            return -1;
        }

        /// <summary>
        /// Find the managed heap section of the specified address.
        /// </summary>
        /// <param name="address">The memory address.</param>
        /// <returns>An index into the snapshot.managedHeapSections array on success, -1 otherwise.</returns>
        public int FindHeapOfAddress(UInt64 address)
        {
            var first = 0;
            var last = managedHeapSections.Length - 1;

            while (first <= last)
            {
                var mid = (first + last) >> 1;
                var section = managedHeapSections[mid];
                var end = section.startAddress + (ulong)section.bytes.Length;

                if (address >= section.startAddress && address < end)
                    return mid;
                else if (address < section.startAddress)
                    last = mid - 1;
                else if (address > section.startAddress)
                    first = mid + 1;
            }

            return -1;
        }

        /// <summary>
        /// Add a connection between two objects, such as a connection from a native object to its managed counter-part.
        /// </summary>
        /// <param name="fromKind">The connection kind, that is pointing to another object.</param>
        /// <param name="fromIndex">An index into a snapshot array, depending on specified fromKind. If the kind would be 'Native', then it must be an index into the snapshot.nativeObjects array.</param>
        /// <param name="toKind">The connection kind, to which the 'from' object is pointing to.</param>
        /// <param name="toIndex">An index into a snapshot array, depending on the specified toKind. If the kind would be 'Native', then it must be an index into the snapshot.nativeObjects array.</param>
        public void AddConnection(PackedConnection.Kind fromKind, int fromIndex, PackedConnection.Kind toKind, int toIndex)
        {
            var connection = new PackedConnection
            {
                fromKind = fromKind,
                from = fromIndex,
                toKind = toKind,
                to = toIndex
            };

            if (connection.fromKind != PackedConnection.Kind.None && connection.from != -1)
            {
                var key = ComputeConnectionKey(connection.fromKind, connection.from);

                List<PackedConnection> list;
                if (!m_ConnectionsFrom.TryGetValue(key, out list))
                    m_ConnectionsFrom[key] = list = new List<PackedConnection>(1); // Capacity=1 to reduce memory usage on HUGE memory snapshots

                list.Add(connection);
            }

            if (connection.toKind != PackedConnection.Kind.None && connection.to != -1)
            {
                if (connection.to < 0)
                    connection.to = -connection.to;

                var key = ComputeConnectionKey(connection.toKind, connection.to);

                List<PackedConnection> list;
                if (!m_ConnectionsTo.TryGetValue(key, out list))
                    m_ConnectionsTo[key] = list = new List<PackedConnection>(1); // Capacity=1 to reduce memory usage on HUGE memory snapshots

                list.Add(connection);
            }
        }

        UInt64 ComputeConnectionKey(PackedConnection.Kind kind, int index)
        {
            var value = (UInt64)(((int)kind << 50) + index);
            return value;
        }

        void GetConnectionsInternal(PackedConnection.Kind kind, int index, List<PackedConnection> references, List<PackedConnection> referencedBy)
        {
            var key = ComputeConnectionKey(kind, index);

            if (references != null)
            {
                List<PackedConnection> refs;
                if (m_ConnectionsFrom.TryGetValue(key, out refs))
                    references.AddRange(refs);
            }

            if (referencedBy != null)
            {
                List<PackedConnection> refsBy;
                if (m_ConnectionsTo.TryGetValue(key, out refsBy))
                    referencedBy.AddRange(refsBy);
            }
        }

        public void GetConnectionsCount(PackedConnection.Kind kind, int index, out int referencesCount, out int referencedByCount)
        {
            referencesCount = 0;
            referencedByCount = 0;

            var key = ComputeConnectionKey(kind, index);

            List<PackedConnection> refs;
            if (m_ConnectionsFrom.TryGetValue(key, out refs))
                referencesCount = refs.Count;

            List<PackedConnection> refBy;
            if (m_ConnectionsTo.TryGetValue(key, out refBy))
                referencedByCount = refBy.Count;
        }

        public void GetConnections(PackedManagedStaticField staticField, List<PackedConnection> references, List<PackedConnection> referencedBy)
        {
            var index = staticField.staticFieldsArrayIndex;
            if (index == -1)
                return;

            GetConnectionsInternal(PackedConnection.Kind.StaticField, index, references, referencedBy);
        }

        public void GetConnections(PackedNativeUnityEngineObject nativeObj, List<PackedConnection> references, List<PackedConnection> referencedBy)
        {
            var index = nativeObj.nativeObjectsArrayIndex;
            if (index == -1)
                return;

            GetConnectionsInternal(PackedConnection.Kind.Native, index, references, referencedBy);
        }

        public void GetConnections(PackedGCHandle gcHandle, List<PackedConnection> references, List<PackedConnection> referencedBy)
        {
            var index = gcHandle.gcHandlesArrayIndex;
            if (index == -1)
                return;

            GetConnectionsInternal(PackedConnection.Kind.GCHandle, index, references, referencedBy);
        }

        public void GetConnections(PackedManagedObject managedObject, List<PackedConnection> references, List<PackedConnection> referencedBy)
        {
            var index = managedObject.managedObjectsArrayIndex;
            if (index == -1)
                return;

            GetConnectionsInternal(PackedConnection.Kind.Managed, index, references, referencedBy);
        }

        public void GetConnections(PackedMemorySection memorySection, List<PackedConnection> references, List<PackedConnection> referencedBy)
        {
            if (memorySection.bytes == null || memorySection.bytes.Length == 0)
                return;

            var startAddress = memorySection.startAddress;
            var endAddress = startAddress + (uint)memorySection.bytes.Length;

            for (int n = 0, nend = managedObjects.Length; n < nend; ++n)
            {
                var mo = managedObjects[n];
                if (mo.address >= startAddress && mo.address < endAddress)
                    references.Add(new PackedConnection() { toKind = PackedConnection.Kind.Managed, to = n });
            }
        }

        public void GetConnectionsCount(PackedMemorySection memorySection, out int referencesCount)
        {
            referencesCount = 0;
            if (memorySection.bytes == null || memorySection.bytes.Length == 0)
                return;

            var startAddress = memorySection.startAddress;
            var endAddress = startAddress + (uint)memorySection.bytes.Length;

            for (int n = 0, nend = managedObjects.Length; n < nend; ++n)
            {
                var mo = managedObjects[n];
                if (mo.address >= startAddress && mo.address < endAddress)
                    referencesCount++;
            }
        }

        // TODO: this costs 500ms in wolf4
        public int FindNativeMonoScriptType(int nativeObjectIndex, out string monoScriptName)
        {
            monoScriptName = "";

            var key = ComputeConnectionKey(PackedConnection.Kind.Native, nativeObjectIndex);

            List<PackedConnection> list;
            if (m_ConnectionsFrom.TryGetValue(key, out list))
            {
                for (int n = 0, nend = list.Count; n < nend; ++n)
                {
                    var connection = list[n];

                    // Check if the connection points to a MonoScript
                    var isPointingToMonoScript = connection.toKind == PackedConnection.Kind.Native && nativeObjects[connection.to].nativeTypesArrayIndex == coreTypes.nativeMonoScript;
                    if (!isPointingToMonoScript)
                        continue;

                    monoScriptName = nativeObjects[connection.to].name;
                    return nativeObjects[connection.to].nativeTypesArrayIndex;
                }
            }

            return -1;
        }

        public bool IsEnum(PackedManagedType type)
        {
            if (!type.isValueType)
                return false;

            if (type.baseOrElementTypeIndex == -1)
                return false;

            if (type.baseOrElementTypeIndex != coreTypes.systemEnum)
                return false;

            return true;
        }

        public bool IsSubclassOf(PackedNativeType type, int baseTypeIndex)
        {
            if (type.nativeTypeArrayIndex == baseTypeIndex)
                return true;

            if (baseTypeIndex < 0 || type.nativeTypeArrayIndex < 0)
                return false;

            var guard = 0;
            while (type.nativeBaseTypeArrayIndex != -1)
            {
                // safety code in case of an infite loop
                if (++guard > 64)
                    break;

                if (type.nativeBaseTypeArrayIndex == baseTypeIndex)
                    return true;

                // get type of the base class
                type = nativeTypes[type.nativeBaseTypeArrayIndex];
            }

            return false;
        }

        public bool IsSubclassOf(PackedManagedType type, int baseTypeIndex)
        {
            if (type.managedTypesArrayIndex == baseTypeIndex)
                return true;

            if (type.managedTypesArrayIndex < 0 || baseTypeIndex < 0)
                return false;

            var guard = 0;
            while (type.baseOrElementTypeIndex != -1)
            {
                // safety code in case of an infite loop
                if (++guard > 64)
                    return false;

                if (type.managedTypesArrayIndex == baseTypeIndex)
                    return true;

                // get type of the base class
                type = managedTypes[type.baseOrElementTypeIndex];
            }

            return false;
        }

        #region Serialization

        [System.Diagnostics.Conditional("ENABLE_PROFILER")]
        static void BeginProfilerSample(string name)
        {
#if ENABLE_PROFILING
        UnityEngine.Profiling.Profiler.BeginSample(name);
#endif
        }

        [System.Diagnostics.Conditional("ENABLE_PROFILER")]
        static void EndProfilerSample()
        {
#if ENABLE_PROFILING
        UnityEngine.Profiling.Profiler.EndSample();
#endif
        }

        void InitializeConnectionsToMonoScripts()
        {
            busyString = "Initializing Connections to MonoScripts";

            var list = new List<int>(256);
            var nativeMonoScriptTypeIndex = coreTypes.nativeMonoScript;

            for (int n = 0, nend = connections.Length; n < nend; ++n)
            {
                // Check if the connection points to a MonoScript
                var isPointingToMonoScript = connections[n].toKind == PackedConnection.Kind.Native && nativeObjects[connections[n].to].nativeTypesArrayIndex == nativeMonoScriptTypeIndex;
                if (isPointingToMonoScript)
                    list.Add(n);
            }

            connectionsToMonoScripts = list.ToArray();
        }

        void InitializeConnections()
        {
            busyString = "Analyzing Object Connections";

            //var fromCount = 0;
            //var toCount = 0;

            for (int n = 0, nend = connections.Length; n < nend; ++n)
            {
                if ((n % (nend / 100)) == 0)
                {
                    var progress = ((n + 1.0f) / nend) * 100;
                    busyString = string.Format("Analyzing Object Connections\n{0}/{1}, {2:F0}% done", n + 1, connections.Length, progress);

                    if (abortActiveStepRequested)
                        break;
                }

                var connection = connections[n];

                AddConnection(connection.fromKind, connection.from, connection.toKind, connection.to);

                //if (connection.fromKind == PackedConnection.Kind.Native || nativeObjects[connection.from].nativeObjectAddress == 0x8E9D4FD0)
                //    fromCount++;
                //if (connection.toKind == PackedConnection.Kind.Native || nativeObjects[connection.to].nativeObjectAddress == 0x8E9D4FD0)
                //    toCount++;
            }

            //Debug.LogFormat("toCount={0}, fromCount={1}", toCount, fromCount);
        }

        void InitializeConnections_OldMemoryProfilingAPI()
        {
            busyString = "Analyzing Object Connections";

            var managedStart = 0;
            var managedEnd = managedStart + gcHandles.Length;
            var nativeStart = managedStart + managedEnd;
            var nativeEnd = nativeStart + nativeObjects.Length;

            //var fromCount = 0;
            //var toCount = 0;

            for (int n = 0, nend = connections.Length; n < nend; ++n)
            {
                if ((n % (nend / 100)) == 0)
                {
                    var progress = ((n + 1.0f) / nend) * 100;
                    busyString = string.Format("Analyzing Object Connections\n{0}/{1}, {2:F0}% done", n + 1, connections.Length, progress);

                    if (abortActiveStepRequested)
                        break;
                }

                var connection = connections[n];

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

                AddConnection(connection.fromKind, connection.from, connection.toKind, connection.to);

                //if (connection.fromKind == PackedConnection.Kind.Native || nativeObjects[connection.from].nativeObjectAddress == 0x8E9D4FD0)
                //    fromCount++;
                //if (connection.toKind == PackedConnection.Kind.Native || nativeObjects[connection.to].nativeObjectAddress == 0x8E9D4FD0)
                //    toCount++;
            }

            //Debug.LogFormat("toCount={0}, fromCount={1}", toCount, fromCount);
        }

        void InitializeManagedHeapSections()
        {
            busyString = "Initializing Managed Heap Sections";

            // sort sections by address. This allows us to use binary search algorithms.
            Array.Sort(managedHeapSections, delegate (PackedMemorySection x, PackedMemorySection y)
            {
                return x.startAddress.CompareTo(y.startAddress);
            });

            for (var n = 0; n < managedHeapSections.Length; ++n)
                managedHeapSections[n].arrayIndex = n;

            // get the aligned memory sections
            var list = new List<PackedMemorySection>(managedHeapSections.Length);
            for (var n=0; n<managedHeapSections.Length; ++n)
            {
                var section = managedHeapSections[n];
                if ((section.startAddress & 4095) == 0)
                    list.Add(section);
            }
            alignedManagedHeapSections = list.ToArray();
        }

        void InitializeManagedFields()
        {
            for (int n = 0, nend = managedTypes.Length; n < nend; ++n)
            {
                var type = managedTypes[n];
                for (int k = 0, kend = type.fields.Length; k < kend; ++k)
                {
                    var name = type.fields[k].name;
                    if (name != null && name[0] == '<')
                    {
                        var index = name.LastIndexOf(">k__BackingField", StringComparison.Ordinal);
                        if (index != -1)
                        {
                            type.fields[k].isBackingField = true;
                            type.fields[k].name = name.Substring(1, index - 1);
                        }
                    }
                }
            }
        }

        void InitializeManagedTypes()
        {
            busyString = "Initializing Managed Types";

            for (int n = 0, nend = managedTypes.Length; n < nend; ++n)
            {
                managedTypes[n].isUnityEngineObject = IsSubclassOf(managedTypes[n], coreTypes.unityEngineObject);
                managedTypes[n].containsFieldOfReferenceType = ContainsFieldOfReferenceType(managedTypes[n]);
                managedTypes[n].containsFieldOfReferenceTypeInInheritenceChain = ContainsFieldOfReferenceTypeInInheritenceChain(managedTypes[n]);
            }
        }

        bool ContainsFieldOfReferenceTypeInInheritenceChain(PackedManagedType type)
        {
            var loopGuard = 0;
            var typeIndex = type.managedTypesArrayIndex;
            while (typeIndex >= 0 && typeIndex < managedTypes.Length)
            {
                if (++loopGuard > 64)
                {
                    break;
                }

                type = managedTypes[typeIndex];
                if (ContainsFieldOfReferenceType(type))
                    return true;

                typeIndex = type.baseOrElementTypeIndex;
            }

            return false;
        }

        bool ContainsFieldOfReferenceType(PackedManagedType type)
        {
            var managedTypesLength = managedTypes.Length;
            var instanceFields = type.instanceFields;

            for (int n = 0, nend = instanceFields.Length; n < nend; ++n)
            {
                var fieldTypeIndex = instanceFields[n].managedTypesArrayIndex;
                if (fieldTypeIndex < 0 || fieldTypeIndex >= managedTypesLength)
                {
                    Error("'{0}' field '{1}' is out of bounds '{2}', ignoring.", type.name, n, fieldTypeIndex);
                    continue;
                }

                var fieldType = managedTypes[instanceFields[n].managedTypesArrayIndex];
                if (!fieldType.isValueType)
                    return true;
            }

            return false;
        }

        void InitializeCoreTypes()
        {
            busyString = "Initializing Core Types";

            for (int n = 0, nend = managedTypes.Length; n < nend; ++n)
            {
                switch (managedTypes[n].name)
                {
                    ///////////////////////////////////////////////////////////////
                    // Primitive types
                    ///////////////////////////////////////////////////////////////

                    case "System.Enum":
                        {
                            coreTypes.systemEnum = n;
                            break;
                        }

                    case "System.Byte":
                        {
                            coreTypes.systemByte = n;
                            break;
                        }

                    case "System.SByte":
                        {
                            coreTypes.systemSByte = n;
                            break;
                        }

                    case "System.Char":
                        {
                            coreTypes.systemChar = n;
                            break;
                        }

                    case "System.Boolean":
                        {
                            coreTypes.systemBoolean = n;
                            break;
                        }

                    case "System.Single":
                        {
                            coreTypes.systemSingle = n;
                            break;
                        }

                    case "System.Double":
                        {
                            coreTypes.systemDouble = n;
                            break;
                        }

                    case "System.Decimal":
                        {
                            coreTypes.systemDecimal = n;
                            break;
                        }

                    case "System.Int16":
                        {
                            coreTypes.systemInt16 = n;
                            break;
                        }

                    case "System.UInt16":
                        {
                            coreTypes.systemUInt16 = n;
                            break;
                        }

                    case "System.Int32":
                        {
                            coreTypes.systemInt32 = n;
                            break;
                        }

                    case "System.UInt32":
                        {
                            coreTypes.systemUInt32 = n;
                            break;
                        }

                    case "System.Int64":
                        {
                            coreTypes.systemInt64 = n;
                            break;
                        }

                    case "System.UInt64":
                        {
                            coreTypes.systemUInt64 = n;
                            break;
                        }

                    case "System.IntPtr":
                        {
                            coreTypes.systemIntPtr = n;
                            break;
                        }

                    case "System.UIntPtr":
                        {
                            coreTypes.systemUIntPtr = n;
                            break;
                        }

                    case "System.String":
                        {
                            coreTypes.systemString = n;
                            break;
                        }

                    case "System.ValueType":
                        {
                            coreTypes.systemValueType = n;
                            break;
                        }

                    case "System.ReferenceType":
                        {
                            coreTypes.systemReferenceType = n;
                            break;
                        }

                    case "System.Object":
                        {
                            coreTypes.systemObject = n;
                            break;
                        }

                    case "System.Delegate":
                        {
                            coreTypes.systemDelegate = n;
                            break;
                        }

                    case "System.MulticastDelegate":
                        {
                            coreTypes.systemMulticastDelegate = n;
                            break;
                        }

                    ///////////////////////////////////////////////////////////////
                    // UnityEngine types
                    ///////////////////////////////////////////////////////////////
                    case "UnityEngine.Object":
                        {
                            coreTypes.unityEngineObject = n;
                            break;
                        }

                    case "UnityEngine.GameObject":
                        {
                            coreTypes.unityEngineGameObject = n;
                            break;
                        }

                    case "UnityEngine.Transform":
                        {
                            coreTypes.unityEngineTransform = n;
                            break;
                        }

                    case "UnityEngine.RectTransform":
                        {
                            coreTypes.unityEngineRectTransform = n;
                            break;
                        }

                    case "UnityEngine.MonoBehaviour":
                        {
                            coreTypes.unityEngineMonoBehaviour = n;
                            break;
                        }

                    case "UnityEngine.Component":
                        {
                            coreTypes.unityEngineComponent = n;
                            break;
                        }

                    case "UnityEngine.ScriptableObject":
                        {
                            coreTypes.unityEngineScriptableObject = n;
                            break;
                        }

                    case "UnityEngine.AssetBundle":
                        {
                            coreTypes.unityEngineScriptableObject = n;
                            break;
                        }
                }
            }

            for (int n = 0, nend = nativeTypes.Length; n < nend; ++n)
            {
                switch (nativeTypes[n].name)
                {
                    case "Object": coreTypes.nativeObject = n; break;
                    case "GameObject": coreTypes.nativeGameObject = n; break;
                    case "MonoBehaviour": coreTypes.nativeMonoBehaviour = n; break;
                    case "ScriptableObject": coreTypes.nativeScriptableObject = n; break;
                    case "Transform": coreTypes.nativeTransform = n; break;
                    case "MonoScript": coreTypes.nativeMonoScript = n; break;
                    case "Component": coreTypes.nativeComponent = n; break;
                    case "AssetBundle": coreTypes.nativeAssetBundle = n; break;
                    case "Texture2D": coreTypes.nativeTexture2D = n; break;
                    case "Texture3D": coreTypes.nativeTexture3D = n; break;
                    case "TextureArray": coreTypes.nativeTextureArray = n; break;
                    case "AudioClip": coreTypes.nativeAudioClip = n; break;
                    case "AnimationClip": coreTypes.nativeAnimationClip = n; break;
                    case "Mesh": coreTypes.nativeMesh = n; break;
                    case "Material": coreTypes.nativeMaterial = n; break;
                    case "Sprite": coreTypes.nativeSprite = n; break;
                    case "Shader": coreTypes.nativeShader = n; break;
                    case "AnimatorController": coreTypes.nativeAnimatorController = n; break;
                    case "Cubemap": coreTypes.nativeCubemap = n; break;
                    case "CubemapArray": coreTypes.nativeCubemapArray = n; break;
                    case "Font": coreTypes.nativeFont = n; break;
                }
            }
        }

        void InitializeNativeTypes()
        {
            busyString = "Processing Native Types";

            for (int n = 0, nend = nativeObjects.Length; n < nend; ++n)
            {
                var nativeTypesArrayIndex = nativeObjects[n].nativeTypesArrayIndex;
                if (nativeTypesArrayIndex < 0)
                    continue;

                nativeTypes[nativeTypesArrayIndex].totalObjectCount += 1;
                nativeTypes[nativeTypesArrayIndex].totalObjectSize += nativeObjects[n].size;
            }

            busyString = "Processing Managed Types";

            for (int n = 0, nend = managedObjects.Length; n < nend; ++n)
            {
                var typeArrayIndex = managedObjects[n].managedTypesArrayIndex;
                if (typeArrayIndex < 0)
                    continue;

                managedTypes[typeArrayIndex].totalObjectCount += 1;
                managedTypes[typeArrayIndex].totalObjectSize += managedObjects[n].size;
            }
        }

        List<ulong> InitializeManagedObjectSubstitudes(string snapshotPath)
        {
            busyString = "Substitude ManagedObjects";

            var substitudeManagedObjects = new List<ulong>();
            if (!string.IsNullOrEmpty(snapshotPath) && System.IO.File.Exists(snapshotPath))
            {
                var p = snapshotPath + ".ManagedObjects.txt";
                if (System.IO.File.Exists(p))
                {
                    var lines = System.IO.File.ReadAllLines(p);
                    for (var n = 0; n < lines.Length; ++n)
                    {
                        var line = lines[n].Trim();
                        if (string.IsNullOrEmpty(line))
                            continue;

                        if (line.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        {
                            line = line.Substring("0x".Length);
                            ulong value;
                            ulong.TryParse(line, System.Globalization.NumberStyles.HexNumber, null, out value);
                            if (value != 0)
                                substitudeManagedObjects.Add(value);
                            else
                                Error("Could not parse '{0}' as hex number.", line);
                        }
                        else
                        {
                            ulong value;
                            ulong.TryParse(line, System.Globalization.NumberStyles.Number, null, out value);
                            if (value != 0)
                                substitudeManagedObjects.Add(value);
                            else
                                Error("Could not parse '{0}' as integer number. If this is a Hex number, prefix it with '0x'.", line);
                        }
                    }
                }
            }

            return substitudeManagedObjects;
        }

        public void Initialize(string snapshotPath = null)
        {
            try
            {
                isProcessing = true;

                BeginProfilerSample("InitializeCoreTypes");
                InitializeCoreTypes();
                abortActiveStepRequested = false;
                EndProfilerSample();

                BeginProfilerSample("InitializeManagedTypes");
                InitializeManagedTypes();
                abortActiveStepRequested = false;
                EndProfilerSample();

                BeginProfilerSample("InitializeManagedFields");
                InitializeManagedFields();
                abortActiveStepRequested = false;
                EndProfilerSample();

                BeginProfilerSample("InitializeManagedHeapSections");
                InitializeManagedHeapSections();
                abortActiveStepRequested = false;
                EndProfilerSample();

                BeginProfilerSample("InitializeConnections");
                if (virtualMachineInformation.heapFormatVersion >= 2019)
                    InitializeConnections();
                else
                    InitializeConnections_OldMemoryProfilingAPI();
                abortActiveStepRequested = false;
                EndProfilerSample();

                BeginProfilerSample("InitializeConnectionsToMonoScripts");
                InitializeConnectionsToMonoScripts();
                abortActiveStepRequested = false;
                EndProfilerSample();

                BeginProfilerSample("substitudeManagedObjects");
                var substitudeManagedObjects = InitializeManagedObjectSubstitudes(snapshotPath);
                EndProfilerSample();

                BeginProfilerSample("InitializeManagedObjects");
                busyString = "Analyzing ManagedObjects";
                var crawler = new PackedManagedObjectCrawler();
                crawler.Crawl(this, substitudeManagedObjects);
                abortActiveStepRequested = false;
                EndProfilerSample();

                InitializeNativeTypes();
                abortActiveStepRequested = false;

                busyString = "Finalizing";
                System.Threading.Thread.Sleep(30);
            }
            catch (System.Exception e)
            {
                Error(e.ToString());
                throw;
            }
            finally
            {
                isProcessing = false;
                isBusy = false;
            }
        }

        #endregion
    }
}
