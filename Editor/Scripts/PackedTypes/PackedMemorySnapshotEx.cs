//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//

//#define ENABLE_PROFILING
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using HeapExplorer.Utilities;
using static HeapExplorer.Utilities.Option;

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
        /// Indices into the <see cref="managedTypes"/> array of static types.
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
        Option<PackedCoreTypes> _coreTypes = None._;
        
        /// <inheritdoc cref="_coreTypes"/>
        public PackedCoreTypes coreTypes => _coreTypes.getOrThrow("core types not initialized");

        /// <summary>
        /// Used to prevent from constantly spamming the Unit log with errors about same things. 
        /// </summary>
        public ConcurrentDictionary<string, Unit> reportedErrors = new ConcurrentDictionary<string, Unit>();

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

        [NonSerialized] Dictionary<UInt64, PInt> m_FindManagedObjectOfNativeObjectLUT;
        [NonSerialized] Dictionary<UInt64, PInt> m_FindManagedTypeOfTypeInfoAddressLUT;
        [NonSerialized] Dictionary<UInt64, PInt> m_FindNativeObjectOfAddressLUT;
        [NonSerialized] Dictionary<UInt64, PackedManagedObject.ArrayIndex> m_FindManagedObjectOfAddressLUT;
        [NonSerialized] Dictionary<ulong, PInt> m_FindGCHandleOfTargetAddressLUT;
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
        /// <returns>
        /// An index into the <see cref="PackedMemorySnapshot.managedTypes"/> array on success, `None` otherwise.
        /// </returns>
        public Option<PInt> FindManagedObjectTypeOfAddress(ulong address) {
            // IL2CPP has the class pointer as the first member of the object.
            {if (FindManagedTypeOfTypeInfoAddress(address).valueOut(out var il2cppAddress))
                return Some(il2cppAddress);}

            // Mono has a vtable pointer as the first member of the object.
            // The first member of the vtable is the class pointer.
            if (!FindHeapOfAddress(address).valueOut(out var heapIndex)) return None._;

            var vtable = managedHeapSections[heapIndex];
            var vtableClassPointerOffset = (int) (address - vtable.startAddress);
            var vtableClassPointer =
                virtualMachineInformation.pointerSize.readPointer(vtable.bytes, vtableClassPointerOffset);
            if (vtableClassPointer == 0) return None._;

            // Mono has a vtable pointer as the first member of the object.
            // The first member of the vtable is the class pointer.
            if (!FindHeapOfAddress(vtableClassPointer).valueOut(out heapIndex)) {
                var maybeHeapIndex = FindManagedTypeOfTypeInfoAddress(vtableClassPointer);
                //Error("Cannot find memory segment for vtableClassPointer pointing at address '{0:X}'.", vtableClassPointer);
                return maybeHeapIndex;
            }

            var typeInfoAddressOffset = (int) (vtableClassPointer - managedHeapSections[heapIndex].startAddress);
            var typeInfoAddressBytes = managedHeapSections[heapIndex].bytes;
            var typeInfoAddress = 
                virtualMachineInformation.pointerSize.readPointer(typeInfoAddressBytes, typeInfoAddressOffset);
            return FindManagedTypeOfTypeInfoAddress(typeInfoAddress);
        }

        /// <summary>
        /// Find the managed object counter-part of a native object.
        /// </summary>
        /// <param name="nativeObjectAddress">The native object address.</param>
        /// <returns>An index into the snapshot.managedObjects array on success, -1 otherwise.</returns>
        public int? FindManagedObjectOfNativeObject(UInt64 nativeObjectAddress)
        {
            if (nativeObjectAddress == 0)
                throw new ArgumentException("address should not be 0", nameof(nativeObjectAddress));

            if (m_FindManagedObjectOfNativeObjectLUT == null)
            {
                m_FindManagedObjectOfNativeObjectLUT = new Dictionary<ulong, PInt>(managedObjects.Length);
                for (PInt n = PInt._0, nend = managedObjects.LengthP(); n < nend; ++n)
                {
                    if (managedObjects[n].nativeObjectsArrayIndex.valueOut(out var nativeObjectsArrayIndex))
                    {
                        var address = nativeObjects[nativeObjectsArrayIndex].nativeObjectAddress;
                        m_FindManagedObjectOfNativeObjectLUT[address] = n;
                    }
                }
            }

            if (m_FindManagedObjectOfNativeObjectLUT.TryGetValue(nativeObjectAddress, out var index))
                return index;

            return null;
        }

        /// <summary>
        /// Find the managed object at the specified address.
        /// </summary>
        /// <param name="managedObjectAddress">The managed object address.</param>
        /// <returns>An index into the snapshot.managedObjects array on success, `None` otherwise.</returns>
        public Option<PackedManagedObject.ArrayIndex> FindManagedObjectOfAddress(UInt64 managedObjectAddress) {
            if (managedObjectAddress == 0)
                return Utils.zeroAddressAccessError<PackedManagedObject.ArrayIndex>(nameof(managedObjectAddress));

            if (m_FindManagedObjectOfAddressLUT == null)
            {
                m_FindManagedObjectOfAddressLUT = new Dictionary<ulong, PackedManagedObject.ArrayIndex>(managedObjects.Length);
                for (PInt n = PInt._0, nend = managedObjects.LengthP(); n < nend; ++n) {
                    m_FindManagedObjectOfAddressLUT[managedObjects[n].address] = PackedManagedObject.ArrayIndex.newObject(n);
                }
            }

            return m_FindManagedObjectOfAddressLUT.get(managedObjectAddress);
        }

        /// <summary>
        /// Find the native object at the specified address.
        /// </summary>
        /// <param name="nativeObjectAddress">The native object address.</param>
        /// <returns>An index into the <see cref="nativeObjects"/> array on success, `None` otherwise.</returns>
        public Option<PInt> FindNativeObjectOfAddress(UInt64 nativeObjectAddress)
        {
            if (nativeObjectAddress == 0) throw new ArgumentException(
                "address can't be 0", nameof(nativeObjectAddress)
            );

            if (m_FindNativeObjectOfAddressLUT == null)
            {
                m_FindNativeObjectOfAddressLUT = new Dictionary<ulong, PInt>(nativeObjects.Length);
                for (PInt n = PInt._0, nend = nativeObjects.LengthP(); n < nend; ++n)
                    m_FindNativeObjectOfAddressLUT[nativeObjects[n].nativeObjectAddress] = n;
            }

            return m_FindNativeObjectOfAddressLUT.get(nativeObjectAddress);
        }

        /// <summary>
        /// Find the GCHandle at the specified address.
        /// </summary>
        /// <param name="targetAddress">The corresponding managed object address.</param>
        /// <returns>An index into the <see cref="gcHandles"/> array on success, `None` otherwise.</returns>
        public Option<PInt> FindGCHandleOfTargetAddress(UInt64 targetAddress)
        {
            if (targetAddress == 0)
                throw new ArgumentException("address should not be 0", nameof(targetAddress));

            if (m_FindGCHandleOfTargetAddressLUT == null)
            {
                m_FindGCHandleOfTargetAddressLUT = new Dictionary<ulong, PInt>(gcHandles.Length);
                for (int n = 0, nend = gcHandles.Length; n < nend; ++n)
                    m_FindGCHandleOfTargetAddressLUT[gcHandles[n].target] = gcHandles[n].gcHandlesArrayIndex;
            }

            return m_FindGCHandleOfTargetAddressLUT.get(targetAddress);
        }

        /// <summary>
        /// Find the managed type of the address where the TypeInfo is stored.
        /// </summary>
        /// <param name="typeInfoAddress">The type info address.</param>
        /// <returns>An index into the <see cref="managedTypes"/> array on success, `None` otherwise.</returns>
        public Option<PInt> FindManagedTypeOfTypeInfoAddress(UInt64 typeInfoAddress)
        {
            if (typeInfoAddress == 0) return Utils.zeroAddressAccessError<PInt>(nameof(typeInfoAddress));

            // Initialize the Look Up Table if it's not initialized.
            if (m_FindManagedTypeOfTypeInfoAddressLUT == null)
            {
                m_FindManagedTypeOfTypeInfoAddressLUT = new Dictionary<ulong, PInt>(managedTypes.Length);
                for (int n = 0, nend = managedTypes.Length; n < nend; ++n)
                    m_FindManagedTypeOfTypeInfoAddressLUT[managedTypes[n].typeInfoAddress] = managedTypes[n].managedTypesArrayIndex;
            }

            return m_FindManagedTypeOfTypeInfoAddressLUT.get(typeInfoAddress);
        }

        /// <summary>
        /// Find the managed heap section of the specified address.
        /// </summary>
        /// <param name="address">The memory address.</param>
        /// <returns>An index into the <see cref="managedHeapSections"/> array on success, `None` otherwise.</returns>
        public Option<int> FindHeapOfAddress(UInt64 address) {
            return binarySearch() 
                   // Debug code - try linear search to see if algorithm is broken.
                   || linearSearch();

            Option<int> binarySearch() {
                var first = 0;
                var last = managedHeapSections.Length - 1;

                while (first <= last) {
                    var mid = (first + last) >> 1;
                    var section = managedHeapSections[mid];

                    if (section.containsAddress(address))
                        return Some(mid);
                    else if (address < section.startAddress)
                        last = mid - 1;
                    else if (address > section.startAddress)
                        first = mid + 1;
                }

                return None._;
            }

            Option<int> linearSearch() {
                var sectionCount = managedHeapSections.Length;
                for (var index = 0; index < sectionCount; index++) {
                    if (managedHeapSections[index].containsAddress(address)) {
                        Debug.LogWarning(
                            $"HeapExplorer: FindHeapOfAddress(0x{address:X}) - binary search has failed, but linear search "
                            + "succeeded, this indicated a bug in the algorithm."
                        );
                        return Some(index);
                    }
                }

                return None._;
            }
        }

        /// <summary>
        /// Add a connection between two objects, such as a connection from a native object to its managed counter-part.
        /// </summary>
        public void AddConnection(PackedConnection.From from, PackedConnection.Pair to) {
            var connection = new PackedConnection(from, to);

            addTo(from.pair.ComputeConnectionKey(), m_ConnectionsFrom);
            addTo(to.ComputeConnectionKey(), m_ConnectionsTo);

            void addTo<K>(K key, Dictionary<K, List<PackedConnection>> dict) {
                var list = dict.getOrUpdate(key, _ => 
                    // Capacity=1 to reduce memory usage on HUGE memory snapshots
                    new List<PackedConnection>(1)
                );
                list.Add(connection);
            }
        }

        void GetConnectionsInternal<TReferences, TReferencedBy>(
            PackedConnection.Pair pair, List<TReferences> references, List<TReferencedBy> referencedBy,
            Func<PackedConnection, TReferences> convertReferences,
            Func<PackedConnection, TReferencedBy> convertReferencedBy
        ) {
            var key = pair.ComputeConnectionKey();

            if (references != null)
            {
                if (m_ConnectionsFrom.TryGetValue(key, out var refs))
                    references.AddRange(refs.Select(convertReferences));
            }

            if (referencedBy != null)
            {
                if (m_ConnectionsTo.TryGetValue(key, out var refsBy))
                    referencedBy.AddRange(refsBy.Select(convertReferencedBy));
            }
        }

        public void GetConnectionsCount(PackedConnection.Pair pair, out int referencesCount, out int referencedByCount)
        {
            referencesCount = 0;
            referencedByCount = 0;

            var key = pair.ComputeConnectionKey();

            if (m_ConnectionsFrom.TryGetValue(key, out var refs))
                referencesCount = refs.Count;

            if (m_ConnectionsTo.TryGetValue(key, out var refBy))
                referencedByCount = refBy.Count;
        }

        public void GetConnections<TReferences, TReferencedBy>(
            PackedManagedStaticField staticField, List<TReferences> references, List<TReferencedBy> referencedBy,
            Func<PackedConnection, TReferences> convertReferences,
            Func<PackedConnection, TReferencedBy> convertReferencedBy
        ) {
            GetConnectionsInternal(
                new PackedConnection.Pair(PackedConnection.Kind.StaticField, staticField.staticFieldsArrayIndex), 
                references, referencedBy, convertReferences, convertReferencedBy
            );
        }
        
        public void GetConnections(
            PackedManagedStaticField staticField, List<PackedConnection> references, List<PackedConnection> referencedBy
        ) => GetConnections(staticField, references, referencedBy, _ => _, _ => _);

        public void GetConnections<TReferences, TReferencedBy>(
            PackedNativeUnityEngineObject nativeObj, List<TReferences> references, List<TReferencedBy> referencedBy,
            Func<PackedConnection, TReferences> convertReferences,
            Func<PackedConnection, TReferencedBy> convertReferencedBy
        ) {
            GetConnectionsInternal(
                new PackedConnection.Pair(PackedConnection.Kind.Native, nativeObj.nativeObjectsArrayIndex),
                references, referencedBy, convertReferences, convertReferencedBy
            );
        }

        public void GetConnections(
            PackedNativeUnityEngineObject nativeObj, List<PackedConnection> references, List<PackedConnection> referencedBy
        ) => GetConnections(nativeObj, references, referencedBy, _ => _, _ => _);

        public void GetConnections<TReferences, TReferencedBy>(
            PackedGCHandle gcHandle, List<TReferences> references, List<TReferencedBy> referencedBy,
            Func<PackedConnection, TReferences> convertReferences,
            Func<PackedConnection, TReferencedBy> convertReferencedBy
        ) {
            GetConnectionsInternal(
                new PackedConnection.Pair(PackedConnection.Kind.GCHandle, gcHandle.gcHandlesArrayIndex), 
                references, referencedBy, convertReferences, convertReferencedBy
            );
        }

        public void GetConnections(
            PackedGCHandle gcHandle, List<PackedConnection> references, List<PackedConnection> referencedBy
        ) => GetConnections(gcHandle, references, referencedBy, _ => _, _ => _);

        public void GetConnections<TReferences, TReferencedBy>(
            PackedManagedObject managedObject, List<TReferences> references, List<TReferencedBy> referencedBy,
            Func<PackedConnection, TReferences> convertReferences,
            Func<PackedConnection, TReferencedBy> convertReferencedBy
        ) {
            GetConnectionsInternal(
                managedObject.managedObjectsArrayIndex.asPair, 
                references, referencedBy, convertReferences, convertReferencedBy
            );
        }

        public void GetConnections(
            PackedManagedObject managedObject, List<PackedConnection> references, List<PackedConnection> referencedBy
        ) => GetConnections(managedObject, references, referencedBy, _ => _, _ => _);

        public void GetConnections<TTarget>(
            PackedMemorySection memorySection, List<TTarget> referencesTo,
            Func<PackedConnection.Pair, TTarget> convert
        ) {
            if (memorySection.bytes == null || memorySection.bytes.Length == 0)
                return;

            var startAddress = memorySection.startAddress;
            var endAddress = startAddress + (uint)memorySection.bytes.Length;

            for (int n = 0, nend = managedObjects.Length; n < nend; ++n)
            {
                var mo = managedObjects[n];
                if (mo.address >= startAddress && mo.address < endAddress)
                    referencesTo.Add(convert(new PackedConnection.Pair(PackedConnection.Kind.Managed, n)));
            }
        }

        public void GetConnections(
            PackedMemorySection memorySection, List<PackedConnection.Pair> referencesTo
        ) => GetConnections(memorySection, referencesTo, _ => _);

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
        public Option<(PInt index, string monoScriptName)> FindNativeMonoScriptType(int nativeObjectIndex)
        {
            var key = new PackedConnection.Pair(PackedConnection.Kind.Native, nativeObjectIndex).ComputeConnectionKey();

            if (!m_ConnectionsFrom.TryGetValue(key, out var list)) return None._;
            for (int n = 0, nend = list.Count; n < nend; ++n)
            {
                var connection = list[n];

                // Check if the connection points to a MonoScript
                var isPointingToMonoScript = 
                    connection.to.kind == PackedConnection.Kind.Native 
                    && nativeObjects[connection.to.index].nativeTypesArrayIndex == coreTypes.nativeMonoScript;
                if (!isPointingToMonoScript)
                    continue;

                var index = nativeObjects[connection.to.index].nativeTypesArrayIndex;
                var monoScriptName = nativeObjects[connection.to.index].name;
                return Some((index, monoScriptName));
            }

            return None._;
        }

        public bool IsEnum(PackedManagedType type)
        {
            if (!type.isValueType)
                return false;

            if (type.baseOrElementTypeIndex.isNone)
                return false;

            if (type.baseOrElementTypeIndex != Some(coreTypes.systemEnum))
                return false;

            return true;
        }

        /// <summary>
        /// Interface for <see cref="PackedMemorySnapshot.IsSubclassOf{T}(HeapExplorer.PackedNativeType,int)"/>.
        /// </summary>
        public interface TypeForSubclassSearch 
        {
            /// <summary>The type name.</summary>
            string name { get; }
            
            /// <summary>Index of the type in the array.</summary>
            PInt typeArrayIndex { get; }
            
            /// <summary>Index of the base type in the array or `None` if this has no base type.</summary>
            Option<PInt> baseTypeArrayIndex { get; }            
        }
        
        public bool IsSubclassOf<T>(T type, T[] array, PInt baseTypeIndex) where T : TypeForSubclassSearch
        {
            var currentType = type;
            
            if (currentType.typeArrayIndex == baseTypeIndex)
                return true;

            if (baseTypeIndex < 0 || currentType.typeArrayIndex < 0)
                return false;

            var cycleTracker = new CycleTracker<PInt>();
            cycleTracker.markStartOfSearch();
            {while (currentType.baseTypeArrayIndex.valueOut(out var baseTypeArrayIndex)) {
                if (cycleTracker.markIteration(baseTypeArrayIndex)) {
                    cycleTracker.reportCycle(
                        $"{nameof(IsSubclassOf)}()", baseTypeArrayIndex,
                        idx => array[idx].ToString()
                    );
                    return false;
                }

                if (baseTypeArrayIndex == baseTypeIndex)
                    return true;

                // get type of the base class
                currentType = array[baseTypeArrayIndex];
            }}

            return false;
        }

        public bool IsSubclassOf(PackedNativeType type, Option<PInt> maybeBaseTypeIndex) => 
            maybeBaseTypeIndex.valueOut(out var baseTypeIndex) && IsSubclassOf(type, baseTypeIndex);

        public bool IsSubclassOf(PackedNativeType type, PInt baseTypeIndex) => 
            IsSubclassOf(type, nativeTypes, baseTypeIndex);

        public bool IsSubclassOf(PackedManagedType type, Option<PInt> maybeBaseTypeIndex) => 
            maybeBaseTypeIndex.valueOut(out var baseTypeIndex) && IsSubclassOf(type, baseTypeIndex);

        public bool IsSubclassOf(PackedManagedType type, PInt baseTypeIndex) => 
            IsSubclassOf(type, managedTypes, baseTypeIndex);

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
                var isPointingToMonoScript = 
                    connections[n].to.kind == PackedConnection.Kind.Native 
                    && nativeObjects[connections[n].to.index].nativeTypesArrayIndex == nativeMonoScriptTypeIndex;
                
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
                    busyString = $"Analyzing Object Connections\n{n + 1}/{connections.Length}, {progress:F0}% done";

                    if (abortActiveStepRequested)
                        break;
                }

                var connection = connections[n];

                AddConnection(connection.from, connection.to);

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
                    busyString = $"Analyzing Object Connections\n{n + 1}/{connections.Length}, {progress:F0}% done";

                    if (abortActiveStepRequested)
                        break;
                }

                var connection = connections[n];

                // Remap the indexes.
                var newFrom = new PackedConnection.From(
                    connection.from.pair.index >= nativeStart && connection.from.pair.index < nativeEnd
                        ? new PackedConnection.Pair(PackedConnection.Kind.Native, connection.from.pair.index - nativeStart)
                        : new PackedConnection.Pair(PackedConnection.Kind.GCHandle, connection.from.pair.index),
                    connection.from.field
                );
                var newTo =
                    connection.to.index >= nativeStart && connection.to.index < nativeEnd
                        ? new PackedConnection.Pair(PackedConnection.Kind.Native, connection.to.index - nativeStart)
                        : new PackedConnection.Pair(PackedConnection.Kind.GCHandle, connection.to.index);

                AddConnection(newFrom, newTo);

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
            Array.Sort(managedHeapSections, (x, y) => x.startAddress.CompareTo(y.startAddress));

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
                    if (!string.IsNullOrEmpty(name) && name[0] == '<')
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
                managedTypes[n].containsFieldOfReferenceTypeInInheritanceChain = ContainsFieldOfReferenceTypeInInheritanceChain(managedTypes[n]);
            }
        }

        bool ContainsFieldOfReferenceTypeInInheritanceChain(PackedManagedType type)
        {
            var currentType = type;
            var cycleTracker = new CycleTracker<int>();
            var maybeTypeIndex = Some(currentType.managedTypesArrayIndex);
            cycleTracker.markStartOfSearch();
            {while (maybeTypeIndex.valueOut(out var typeIndex)) {
                if (cycleTracker.markIteration(typeIndex)) {
                    cycleTracker.reportCycle(
                        $"{nameof(ContainsFieldOfReferenceTypeInInheritanceChain)}({type.name})", typeIndex,
                        idx => managedTypes[idx].ToString()
                    );
                    break;
                }

                currentType = managedTypes[typeIndex];
                if (ContainsFieldOfReferenceType(currentType))
                    return true;

                maybeTypeIndex = currentType.baseOrElementTypeIndex;
            }}

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
            if (PackedCoreTypes.tryInitialize(managedTypes, nativeTypes, out var coreTypes).valueOut(out var errors)) {
                var errorsStr = string.Join("\n", errors);
                throw new Exception($"Can't initialize core types, this should never happen! Errors:\n{errorsStr}");
            }
            _coreTypes = Some(coreTypes);
        }

        void InitializeNativeTypes()
        {
            busyString = "Processing Native Types";

            for (PInt n = PInt._0, nend = nativeObjects.LengthP(); n < nend; ++n)
            {
                var nativeTypesArrayIndex = nativeObjects[n].nativeTypesArrayIndex;
                if (nativeTypesArrayIndex < 0)
                    continue;

                nativeTypes[nativeTypesArrayIndex].totalObjectCount += PInt._1;
                nativeTypes[nativeTypesArrayIndex].totalObjectSize += nativeObjects[n].size;
            }

            busyString = "Processing Managed Types";

            for (PInt n = PInt._0, nend = managedObjects.LengthP(); n < nend; ++n)
            {
                var typeArrayIndex = managedObjects[n].managedTypesArrayIndex;
                if (typeArrayIndex < 0)
                    continue;

                managedTypes[typeArrayIndex].totalObjectCount += PInt._1;
                managedTypes[typeArrayIndex].totalObjectSize += managedObjects[n].size.getOrElse(0);
            }
        }

        List<ulong> InitializeManagedObjectSubstitutes(string snapshotPath)
        {
            busyString = "Substitute ManagedObjects";

            var substituteManagedObjects = new List<ulong>();
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
                                substituteManagedObjects.Add(value);
                            else
                                Error("Could not parse '{0}' as hex number.", line);
                        }
                        else
                        {
                            ulong value;
                            ulong.TryParse(line, System.Globalization.NumberStyles.Number, null, out value);
                            if (value != 0)
                                substituteManagedObjects.Add(value);
                            else
                                Error("Could not parse '{0}' as integer number. If this is a Hex number, prefix it with '0x'.", line);
                        }
                    }
                }
            }

            return substituteManagedObjects;
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

                BeginProfilerSample("substituteManagedObjects");
                var substituteManagedObjects = InitializeManagedObjectSubstitutes(snapshotPath);
                EndProfilerSample();

                BeginProfilerSample("InitializeManagedObjects");
                busyString = "Analyzing ManagedObjects";
                var crawler = new PackedManagedObjectCrawler();
                crawler.Crawl(this, substituteManagedObjects);
                abortActiveStepRequested = false;
                EndProfilerSample();

                InitializeNativeTypes();
                abortActiveStepRequested = false;

                busyString = "Finalizing";
                Thread.Sleep(30);
            }
            catch (Exception e)
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
