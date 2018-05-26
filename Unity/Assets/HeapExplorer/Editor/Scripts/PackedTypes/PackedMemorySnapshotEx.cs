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
        [NonSerialized]
        public PackedManagedObject[] managedObjects = new PackedManagedObject[0];

        [NonSerialized]
        public PackedManagedStaticField[] managedStaticFields = new PackedManagedStaticField[0];

        [NonSerialized]
        ulong m_managedHeapSize = ~0ul;

        // The size of the managed heap
        public ulong managedHeapSize
        {
            get
            {
                if (m_managedHeapSize != ~0ul)
                    return m_managedHeapSize;

                for (int n = 0, nend = managedHeapSections.Length; n < nend; ++n)
                    m_managedHeapSize += managedHeapSections[n].size;

                return m_managedHeapSize;
            }
        }

        [NonSerialized]
        ulong m_managedHeapAddressSpace = ~0ul;

        // The address space from the operating system in which the managed heap sections are located.
        public ulong managedHeapAddressSpace
        {
            get
            {
                if (m_managedHeapAddressSpace != ~0ul)
                    return m_managedHeapAddressSpace;

                if (managedHeapSections.Length == 0)
                    return m_managedHeapAddressSpace = 0;

                var first = managedHeapSections[0].startAddress;
                var last = managedHeapSections[managedHeapSections.Length - 1].startAddress + managedHeapSections[managedHeapSections.Length - 1].size;
                m_managedHeapAddressSpace = last - first;
                return m_managedHeapAddressSpace;
            }
        }

        // Indexes into the managedTypes array of static types
        [NonSerialized]
        public int[] managedStaticTypes = new int[0];

        // Index in connections array
        [NonSerialized]
        public int[] connectionsToMonoScripts = new int[0];

        [NonSerialized]
        public PackedCoreTypes coreTypes = new PackedCoreTypes();

        [NonSerialized]
        public List<string> errors = new List<string>(256);

        [NonSerialized]
        public string stateString = "";

        [NonSerialized] Dictionary<UInt64, int> m_findManagedObjectOfNativeObjectLUT;
        [NonSerialized] Dictionary<UInt64, int> m_findManagedTypeOfTypeInfoAddressLUT;
        [NonSerialized] Dictionary<UInt64, int> m_findNativeObjectOfAddressLUT;
        [NonSerialized] Dictionary<UInt64, int> m_findManagedObjectOfAddressLUT;
        [NonSerialized] Dictionary<ulong, int> m_findGCHandleOfTargetAddressLUT;

        [NonSerialized] Dictionary<UInt64, List<PackedConnection>> m_connectionsFrom = new Dictionary<ulong, List<PackedConnection>>(1024*32);
        [NonSerialized] Dictionary<UInt64, List<PackedConnection>> m_connectionsTo = new Dictionary<ulong, List<PackedConnection>>(1024 * 32);

        public bool isReady
        {
            get;
            private set;
        }

        //bool m_abort;
        public void Abort()
        {
            //m_abort = true;
        }

        public void Error(string format, params object[] args)
        {
            //Debug.LogErrorFormat(format, args);
            errors.Add(string.Format(format, args));
        }

        public void FindManagedStaticFieldsOfType(PackedManagedType type, List<int> target)
        {
            if (target == null)
                return;

            for (int n=0, nend = managedStaticFields.Length; n < nend; ++n)
            {
                if (managedStaticFields[n].managedTypesArrayIndex == type.managedTypesArrayIndex)
                    target.Add(managedStaticFields[n].staticFieldsArrayIndex);
            }
        }

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

        //public int FindManagedObjectTypeOfAddress(System.UInt64 address)
        //{
        //    var heapIndex = FindHeapOfAddress(address);
        //    if (heapIndex == -1)
        //    {
        //        Error("Cannot find memory segment for address '{0:X}'.", address);
        //        return -1;
        //    }

        //    var typeIndex = FindManagedTypeOfTypeInfoAddress(managedHeapSections[heapIndex].startAddress);
        //    if (typeIndex == -1)
        //    {
        //        var vtable = managedHeapSections[heapIndex];// heap.Find(objectAddress, _virtualMachineInformation);
        //        var offset = (int)(address - vtable.startAddress);

        //        var vtableClassPointer = virtualMachineInformation.pointerSize == 8 ? BitConverter.ToUInt64(vtable.bytes, offset) : BitConverter.ToUInt32(vtable.bytes, offset);
        //        if (vtableClassPointer == 0)
        //        {
        //            Error("Cannot find memory segment for address '{0:X}', because the 'vtableClassPointer' points to NULL.", address);
        //            return -1;
        //        }

        //        // IL2CPP has the class pointer as the first member of the object.
        //        //var il2cppType = FindManagedTypeOfTypeInfoAddress(vtableClassPointer);
        //        //if (il2cppType != -1)
        //        //    return il2cppType;

        //        // Mono has a vtable pointer as the first member of the object.
        //        // The first member of the vtable is the class pointer.
        //        heapIndex = FindHeapOfAddress(vtableClassPointer);
        //        if (heapIndex == -1)
        //        {
        //            Error("Cannot find memory segment for vtableClassPointer pointing at address '{0:X}'.", vtableClassPointer);
        //            return -1;
        //        }

        //        offset = (int)(vtableClassPointer - managedHeapSections[heapIndex].startAddress);

        //        if (virtualMachineInformation.pointerSize == 8)
        //            typeIndex = FindManagedTypeOfTypeInfoAddress(BitConverter.ToUInt64(managedHeapSections[heapIndex].bytes, offset));
        //        else if (virtualMachineInformation.pointerSize == 4)
        //            typeIndex = FindManagedTypeOfTypeInfoAddress(BitConverter.ToUInt32(managedHeapSections[heapIndex].bytes, offset));
        //    }

        //    return typeIndex;
        //}

        public int FindManagedObjectOfNativeObject(UInt64 nativeObjectAddress)
        {
            if (nativeObjectAddress == 0)
                return -1;

            if (m_findManagedObjectOfNativeObjectLUT == null)
            {
                m_findManagedObjectOfNativeObjectLUT = new Dictionary<ulong, int>(managedObjects.Length);
                for (int n = 0, nend = managedObjects.Length; n < nend; ++n)
                {
                    if (managedObjects[n].nativeObjectsArrayIndex >= 0)
                    {
                        var address = (ulong)nativeObjects[managedObjects[n].nativeObjectsArrayIndex].nativeObjectAddress;
                        m_findManagedObjectOfNativeObjectLUT[address] = n;
                    }
                }
            }

            int index;
            if (m_findManagedObjectOfNativeObjectLUT.TryGetValue(nativeObjectAddress, out index))
                return index;

            return -1;
        }

        public int FindManagedObjectOfAddress(UInt64 managedObjectAddress)
        {
            if (managedObjectAddress == 0)
                return -1;

            if (m_findManagedObjectOfAddressLUT == null)
            {
                m_findManagedObjectOfAddressLUT = new Dictionary<ulong, int>(managedObjects.Length);
                for (int n = 0, nend = managedObjects.Length; n < nend; ++n)
                    m_findManagedObjectOfAddressLUT[managedObjects[n].address] = n;
            }

            int index;
            if (m_findManagedObjectOfAddressLUT.TryGetValue(managedObjectAddress, out index))
                return index;

            return -1;
        }

        public int FindNativeObjectOfAddress(UInt64 nativeObjectAddress)
        {
            if (nativeObjectAddress == 0)
                return -1;

            if (m_findNativeObjectOfAddressLUT == null)
            {
                m_findNativeObjectOfAddressLUT = new Dictionary<ulong, int>(nativeObjects.Length);
                for (int n = 0, nend = nativeObjects.Length; n < nend; ++n)
                    m_findNativeObjectOfAddressLUT[(ulong)nativeObjects[n].nativeObjectAddress] = n;
            }

            int index;
            if (m_findNativeObjectOfAddressLUT.TryGetValue(nativeObjectAddress, out index))
                return index;

            return -1;
        }
        
        public int FindGCHandleOfTargetAddress(UInt64 targetAddress)
        {
            if (targetAddress == 0)
                return -1;

            if (m_findGCHandleOfTargetAddressLUT == null)
            {
                m_findGCHandleOfTargetAddressLUT = new Dictionary<ulong, int>(gcHandles.Length);
                for (int n = 0, nend = gcHandles.Length; n < nend; ++n)
                    m_findGCHandleOfTargetAddressLUT[gcHandles[n].target] = gcHandles[n].gcHandlesArrayIndex;
            }

            int index;
            if (m_findGCHandleOfTargetAddressLUT.TryGetValue(targetAddress, out index))
                return index;

            return -1;
        }

        public int FindManagedTypeOfTypeInfoAddress(UInt64 typeInfoAddress)
        {
            if (typeInfoAddress == 0)
                return -1;

            if (m_findManagedTypeOfTypeInfoAddressLUT == null)
            {
                m_findManagedTypeOfTypeInfoAddressLUT = new Dictionary<ulong, int>(managedTypes.Length);
                for (int n = 0, nend = managedTypes.Length; n < nend; ++n)
                    m_findManagedTypeOfTypeInfoAddressLUT[managedTypes[n].typeInfoAddress] = managedTypes[n].managedTypesArrayIndex;
            }

            int index;
            if (m_findManagedTypeOfTypeInfoAddressLUT.TryGetValue(typeInfoAddress, out index))
                return index;

            return -1;
        }

        public int FindManagedTypeOfName(string typeName, StringComparison comparison)
        {
            if (string.IsNullOrEmpty(typeName))
                return -1;

            for (int n=0, nend = managedTypes.Length; n < nend; ++n)
            {
                if (string.Equals(managedTypes[n].name, typeName, comparison))
                    return n;
            }

            return -1;
        }

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

        UInt64 ComputeConnectionKey(PackedConnection.Kind kind, int index)
        {
            var value = (UInt64)(((int)kind << 50) + index);
            return value;
        }

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
                if (!m_connectionsFrom.TryGetValue(key, out list))
                    m_connectionsFrom[key] = list = new List<PackedConnection>(1); // Capacity=1 to reduce memory usage on HUGE memory snapshots

                list.Add(connection);
            }

            if (connection.toKind != PackedConnection.Kind.None && connection.to != -1)
            {
                if (connection.to < 0)
                    connection.to = -connection.to;

                var key = ComputeConnectionKey(connection.toKind, connection.to);

                List<PackedConnection> list;
                if (!m_connectionsTo.TryGetValue(key, out list))
                    m_connectionsTo[key] = list = new List<PackedConnection>(1); // Capacity=1 to reduce memory usage on HUGE memory snapshots

                list.Add(connection);
            }
        }
        void GetConnectionsInternal(PackedConnection.Kind kind, int index, List<PackedConnection> references, List<PackedConnection> referencedBy)
        {
            var key = ComputeConnectionKey(kind, index);

            if (references != null)
            {
                List<PackedConnection> refs;
                if (m_connectionsFrom.TryGetValue(key, out refs))
                    references.AddRange(refs);
            }

            if (referencedBy != null)
            {
                List<PackedConnection> refsBy;
                if (m_connectionsTo.TryGetValue(key, out refsBy))
                    referencedBy.AddRange(refsBy);
            }
        }

        public void GetConnectionsCount(PackedConnection.Kind kind, int index, out int referencesCount, out int referencedByCount)
        {
            referencesCount = 0;
            referencedByCount = 0;
            
            var key = ComputeConnectionKey(kind, index);

            List<PackedConnection> refs;
            if (m_connectionsFrom.TryGetValue(key, out refs))
                referencesCount = refs.Count;

            List<PackedConnection> refBy;
            if (m_connectionsTo.TryGetValue(key, out refBy))
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
            if (m_connectionsFrom.TryGetValue(key, out list))
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
            stateString = "Initializing Connections to MonoScripts";

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
            stateString = "Analyzing Object Connections";
            
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
                    stateString = string.Format("Analyzing {0} Object Connections, {1:F0}% done", connections.Length, progress);
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
            stateString = "Initializing Managed Heap Sections";

            // sort sections by address. This allows us to use binary search algorithms.
            Array.Sort(managedHeapSections, delegate (PackedMemorySection x, PackedMemorySection y)
            {
                return x.startAddress.CompareTo(y.startAddress);
            });
        }

        void InitializeManagedFields()
        {
            for (int n = 0, nend = managedTypes.Length; n < nend; ++n)
            {
                var type = managedTypes[n];
                for (int k=0, kend=type.fields.Length; k < kend; ++k)
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
            stateString = "Initializing Managed Types";

            for (int n = 0, nend = managedTypes.Length; n < nend; ++n)
            {
                managedTypes[n].isUnityEngineObject = IsSubclassOf(managedTypes[n], coreTypes.unityEngineObject);
            }
        }

        void InitializeCoreTypes()
        {
            stateString = "Initializing Core Types";

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
                            coreTypes.unityEngineTransform = n;
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
                }
            }
        }
        
        void InitializeNativeTypes()
        {
            stateString = "Processing Native Types";

            for (int n=0, nend = nativeObjects.Length; n < nend; ++n)
            {
                var nativeTypesArrayIndex = nativeObjects[n].nativeTypesArrayIndex;
                if (nativeTypesArrayIndex < 0)
                    continue;

                nativeTypes[nativeTypesArrayIndex].totalObjectCount += 1;
                nativeTypes[nativeTypesArrayIndex].totalObjectSize += nativeObjects[n].size;
            }

            stateString = "Processing Managed Types";

            for (int n = 0, nend = managedObjects.Length; n < nend; ++n)
            {
                var typeArrayIndex = managedObjects[n].managedTypesArrayIndex;
                if (typeArrayIndex < 0)
                    continue;

                managedTypes[typeArrayIndex].totalObjectCount += 1;
                managedTypes[typeArrayIndex].totalObjectSize += managedObjects[n].size;
            }
        }

        public void Initialize()
        {
            try
            {
                BeginProfilerSample("InitializeCoreTypes");
                InitializeCoreTypes();
                EndProfilerSample();

                BeginProfilerSample("InitializeManagedTypes");
                InitializeManagedTypes();
                EndProfilerSample();

                BeginProfilerSample("InitializeManagedFields");
                InitializeManagedFields();
                EndProfilerSample();

                BeginProfilerSample("InitializeManagedHeapSections");
                InitializeManagedHeapSections();
                EndProfilerSample();

                BeginProfilerSample("InitializeConnections");
                InitializeConnections();
                EndProfilerSample();

                BeginProfilerSample("InitializeConnectionsToMonoScripts");
                InitializeConnectionsToMonoScripts();
                EndProfilerSample();

                BeginProfilerSample("InitializeManagedObjects");
                stateString = "Analyzing ManagedObjects";
                var crawler = new PackedManagedObjectCrawler();
                crawler.Crawl(this);
                EndProfilerSample();

                InitializeNativeTypes();

                stateString = "Finalizing";
                System.Threading.Thread.Sleep(30);
                isReady = true;
            }
            catch (System.Exception e)
            {
                Error(e.ToString());
                throw;
            }
        }

#endregion
    }
}
