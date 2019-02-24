//
// Heap Explorer for Unity. Copyright (c) 2019 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://bitbucket.org/pschraut/unityheapexplorer/
//

//#define DEBUG_BREAK_ON_ADDRESS
//#define ENABLE_PROFILING
//#define ENABLE_PROFILER
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

namespace HeapExplorer
{
    public class PackedManagedObjectCrawler
    {
        public static bool s_IgnoreNestedStructs = true;

        long m_TotalCrawled;
        List<PackedManagedObject> m_Crawl = new List<PackedManagedObject>(1024 * 1024);
        Dictionary<ulong, int> m_Seen = new Dictionary<ulong, int>(1024 * 1024);
        List<PackedManagedObject> m_ManagedObjects = new List<PackedManagedObject>(1024 * 1024);
        List<PackedManagedStaticField> m_StaticFields = new List<PackedManagedStaticField>(10 * 1024);
        PackedMemorySnapshot m_Snapshot;
        PackedManagedField m_CachedPtr;
        MemoryReader m_MemoryReader;

#if DEBUG_BREAK_ON_ADDRESS
        ulong DebugBreakOnAddress = 0x2604C8AFEE0;
#endif

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

        public void Crawl(PackedMemorySnapshot snapshot, List<ulong> substitudeAddresses)
        {
            m_TotalCrawled = 0;
            m_Snapshot = snapshot;
            m_MemoryReader = new MemoryReader(m_Snapshot);
            InitializeCachedPtr();

            BeginProfilerSample("CrawlGCHandles");
            CrawlGCHandles();
            EndProfilerSample();

            BeginProfilerSample("substitudeAddresses");
            for (var n = 0; n < substitudeAddresses.Count; ++n)
            {
                var addr = substitudeAddresses[n];
                if (m_Seen.ContainsKey(addr))
                    continue;
                TryAddManagedObject(addr);
            }
            EndProfilerSample();

            BeginProfilerSample("CrawlStatic");
            CrawlStatic();
            m_Snapshot.managedStaticFields = m_StaticFields.ToArray();
            EndProfilerSample();

            BeginProfilerSample("CrawlManagedObjects");
            CrawlManagedObjects();

            m_Snapshot.managedObjects = m_ManagedObjects.ToArray();
            UpdateProgress();
            EndProfilerSample();
        }

        void InitializeCachedPtr()
        {
            var unityEngineObject = m_Snapshot.managedTypes[m_Snapshot.coreTypes.unityEngineObject];

            // UnityEngine.Object types on the managed side have a m_CachedPtr field that
            // holds the native memory address of the corresponding native object of this managed object.
            m_CachedPtr = new PackedManagedField();

            for (int n = 0, nend = unityEngineObject.fields.Length; n < nend; ++n)
            {
                var field = unityEngineObject.fields[n];
                if (field.name != "m_CachedPtr")
                    continue;

                m_CachedPtr = field;
                return;
            }
        }

        bool ContainsReferenceType(int typeIndex)
        {
            var baseType = m_Snapshot.managedTypes[typeIndex];
            if (!baseType.isValueType)
                return true;

            var managedTypesLength = m_Snapshot.managedTypes.Length;
            var instanceFields = baseType.instanceFields;

            for (int n=0, nend = instanceFields.Length; n < nend; ++n)
            {
                var fieldTypeIndex = instanceFields[n].managedTypesArrayIndex;
                if (fieldTypeIndex < 0 || fieldTypeIndex >= managedTypesLength)
                {
                    m_Snapshot.Error("'{0}' field '{1}' is out of bounds '{2}', ignoring.", baseType.name, n, fieldTypeIndex);
                    continue;
                }

                var fieldType = m_Snapshot.managedTypes[instanceFields[n].managedTypesArrayIndex];
                if (!fieldType.isValueType)
                    return true;
            }

            return false;
        }

        void CrawlManagedObjects()
        {
            var virtualMachineInformation = m_Snapshot.virtualMachineInformation;
            var nestedStructsIgnored = 0;

            //var guard = 0;
            while (m_Crawl.Count > 0)
            {
                //if (++guard > 10000000)
                //{
                //    Debug.LogWarning("Loop guard kicked in");
                //    break;
                //}
                if ((m_TotalCrawled % 1000) == 0)
                {
                    UpdateProgress();
                    if (m_Snapshot.abortActiveStepRequested)
                        break;
                }

                var mo = m_Crawl[m_Crawl.Count - 1];
                m_Crawl.RemoveAt(m_Crawl.Count - 1);

#if DEBUG_BREAK_ON_ADDRESS
                if (mo.address == DebugBreakOnAddress)
                {
                    int a = 0;
                }
#endif

                var loopGuard = 0;
                var typeIndex = mo.managedTypesArrayIndex;

                while (typeIndex != -1)
                {
                    if (++loopGuard > 264)
                        break;

                    AbstractMemoryReader memoryReader = m_MemoryReader;
                    if (mo.staticBytes != null)
                        memoryReader = new StaticMemoryReader(m_Snapshot, mo.staticBytes);

                    var baseType = m_Snapshot.managedTypes[typeIndex];

                    if (baseType.isArray)
                    {
                        if (baseType.baseOrElementTypeIndex < 0 || baseType.baseOrElementTypeIndex >= m_Snapshot.managedTypes.Length)
                        {
                            m_Snapshot.Error("'{0}.baseOrElementTypeIndex' = {1} at address '{2:X}', ignoring managed object.", baseType.name, baseType.baseOrElementTypeIndex, mo.address);
                            break;
                        }

                        var elementType = m_Snapshot.managedTypes[baseType.baseOrElementTypeIndex];
                        if (elementType.isValueType && elementType.isPrimitive)
                            break; // don't crawl int[], byte[], etc

                        if (elementType.isValueType && !ContainsReferenceType(elementType.managedTypesArrayIndex))
                            break;

                        var dim0Length = mo.address > 0 ? memoryReader.ReadArrayLength(mo.address, baseType) : 0;
                        //if (dim0Length > 1024 * 1024)
                        if (dim0Length > (32*1024) * (32*1024))
                        {
                            m_Snapshot.Error("Array (rank={2}) found at address '{0:X} with '{1}' elements, that doesn't seem right.", mo.address, dim0Length, baseType.arrayRank);
                            break;
                        }

                        for (var k = 0; k < dim0Length; ++k)
                        {
                            if ((m_TotalCrawled % 1000) == 0)
                                UpdateProgress();

                            ulong elementAddr = 0;

                            if (elementType.isArray)
                            {
                                elementAddr = memoryReader.ReadPointer(mo.address + (ulong)(k * virtualMachineInformation.pointerSize) + (ulong)virtualMachineInformation.arrayHeaderSize);
                            }
                            else if (elementType.isValueType)
                            {
                                elementAddr = mo.address + (ulong)(k * elementType.size) + (ulong)virtualMachineInformation.arrayHeaderSize - (ulong)virtualMachineInformation.objectHeaderSize;
                            }
                            else
                            {
                                elementAddr = memoryReader.ReadPointer(mo.address + (ulong)(k * virtualMachineInformation.pointerSize) + (ulong)virtualMachineInformation.arrayHeaderSize);
                            }

#if DEBUG_BREAK_ON_ADDRESS
                            if (elementAddr == DebugBreakOnAddress)
                            {
                                int a = 0;
                            }
#endif

                            if (elementAddr != 0)
                            {
                                int newObjectIndex;
                                if (!m_Seen.TryGetValue(elementAddr, out newObjectIndex))
                                {
                                    var newObj = PackedManagedObject.New();
                                    newObj.address = elementAddr;
                                    newObj.managedTypesArrayIndex = elementType.managedTypesArrayIndex;
                                    newObj.managedObjectsArrayIndex = m_ManagedObjects.Count;

                                    if (elementType.isValueType)
                                    {
                                        newObj.managedObjectsArrayIndex = mo.managedObjectsArrayIndex;
                                        newObj.staticBytes = mo.staticBytes;
                                    }
                                    else
                                    {
                                        newObj.managedTypesArrayIndex = m_Snapshot.FindManagedObjectTypeOfAddress(elementAddr);
                                        if (newObj.managedTypesArrayIndex == -1)
                                            newObj.managedTypesArrayIndex = elementType.managedTypesArrayIndex;

                                        TryConnectNativeObject(ref newObj);
                                    }
                                    SetObjectSize(ref newObj, m_Snapshot.managedTypes[newObj.managedTypesArrayIndex]);

                                    if (!elementType.isValueType)
                                        m_ManagedObjects.Add(newObj);

                                    m_Seen[newObj.address] = newObj.managedObjectsArrayIndex;
                                    m_Crawl.Add(newObj);
                                    m_TotalCrawled++;
                                    newObjectIndex = newObj.managedObjectsArrayIndex;
                                }

                                // If we do not connect the Slot elements at Slot[] 0x1DB2A512EE0
                                if (!elementType.isValueType)
                                {
                                    if (mo.managedObjectsArrayIndex >= 0)
                                        m_Snapshot.AddConnection(PackedConnection.Kind.Managed, mo.managedObjectsArrayIndex, PackedConnection.Kind.Managed, newObjectIndex);
                                    else
                                        m_Snapshot.AddConnection(PackedConnection.Kind.StaticField, -mo.managedObjectsArrayIndex, PackedConnection.Kind.Managed, newObjectIndex);
                                }
                            }
                        }

                        break;
                    }

                    for (var n = 0; n < baseType.fields.Length; ++n)
                    {
                        var field = baseType.fields[n];
                        if (field.isStatic)
                            continue;

                        var fieldType = m_Snapshot.managedTypes[field.managedTypesArrayIndex];

                        if (fieldType.isValueType)
                        {
                            if (fieldType.isPrimitive)
                                continue;

                            if (s_IgnoreNestedStructs && mo.managedTypesArrayIndex == fieldType.managedTypesArrayIndex)
                            {
                                nestedStructsIgnored++;
                                continue;
                            }

                            var newObj = PackedManagedObject.New();

                            if (mo.staticBytes == null)
                                newObj.address = mo.address + (uint)field.offset - (uint)virtualMachineInformation.objectHeaderSize;
                            else
                                newObj.address = mo.address + (uint)field.offset - (uint)virtualMachineInformation.objectHeaderSize;

                            newObj.managedObjectsArrayIndex = mo.managedObjectsArrayIndex;
                            newObj.managedTypesArrayIndex = fieldType.managedTypesArrayIndex;
                            newObj.staticBytes = mo.staticBytes;
                            SetObjectSize(ref newObj, fieldType);

                            m_Crawl.Add(newObj); // Crawl, but do not add value types to the managedlist
                            m_TotalCrawled++;
                            continue;
                        }

                        if (!fieldType.isValueType)
                        {
                            ulong addr = 0;
                            if (mo.staticBytes == null)
                                addr = memoryReader.ReadPointer(mo.address + (uint)field.offset);
                            else
                                addr = memoryReader.ReadPointer(mo.address + (uint)field.offset - (uint)virtualMachineInformation.objectHeaderSize);

                            if (addr == 0)
                                continue;

#if DEBUG_BREAK_ON_ADDRESS
                            if (addr == DebugBreakOnAddress)
                            {
                                int a = 0;
                            }
#endif

                            int newObjIndex;
                            if (!m_Seen.TryGetValue(addr, out newObjIndex))
                            {
                                var newObj = PackedManagedObject.New();
                                newObj.address = addr;
                                newObj.managedObjectsArrayIndex = m_ManagedObjects.Count;
                                newObj.managedTypesArrayIndex = m_Snapshot.FindManagedObjectTypeOfAddress(addr);
                                if (newObj.managedTypesArrayIndex == -1)
                                    newObj.managedTypesArrayIndex = fieldType.managedTypesArrayIndex;

                                SetObjectSize(ref newObj, m_Snapshot.managedTypes[newObj.managedTypesArrayIndex]);
                                TryConnectNativeObject(ref newObj);

                                m_Seen[newObj.address] = newObj.managedObjectsArrayIndex;
                                m_ManagedObjects.Add(newObj);
                                m_Crawl.Add(newObj);
                                m_TotalCrawled++;
                                newObjIndex = newObj.managedObjectsArrayIndex;
                            }

                            if (mo.managedObjectsArrayIndex >= 0)
                                m_Snapshot.AddConnection(PackedConnection.Kind.Managed, mo.managedObjectsArrayIndex, PackedConnection.Kind.Managed, newObjIndex);
                            else
                                m_Snapshot.AddConnection(PackedConnection.Kind.StaticField, -mo.managedObjectsArrayIndex, PackedConnection.Kind.Managed, newObjIndex);

                            continue;
                        }
                    }
                    
                    if (typeIndex == baseType.baseOrElementTypeIndex || baseType.isArray)
                        break;

                    typeIndex = baseType.baseOrElementTypeIndex;
                }
            }

            if (nestedStructsIgnored > 0)
                m_Snapshot.Warning("HeapExplorer: {0} nested structs ignored (Workaround for Unity bug Case 1104590).", nestedStructsIgnored);
        }

        void CrawlStatic()
        {
            var crawlStatic = new List<int>();
            var managedTypes = m_Snapshot.managedTypes;
            var staticManagedTypes = new List<int>(1024);

            // Unity BUG: (Case 984330) PackedMemorySnapshot: Type contains staticFieldBytes, but has no static field
            for (int n = 0, nend = managedTypes.Length; n < nend; ++n)
            {
                var type = managedTypes[n];

                // Some static classes have no staticFieldBytes. As I understand this, the staticFieldBytes
                // are only filled if that static class has been initialized (its cctor called), otherwise it's zero.
                if (type.staticFieldBytes == null || type.staticFieldBytes.Length == 0)
                    continue;

                //var hasStaticField = false;
                for (int j = 0, jend = type.fields.Length; j < jend; ++j)
                {
                    if (!type.fields[j].isStatic)
                        continue;
                    
                    //var field = type.fields[j];
                    //var fieldType = managedTypes[field.managedTypesArrayIndex];
                    //hasStaticField = true;

                    var item = new PackedManagedStaticField
                    {
                        managedTypesArrayIndex = type.managedTypesArrayIndex,
                        fieldIndex = j,
                        staticFieldsArrayIndex = m_StaticFields.Count,
                    };
                    m_StaticFields.Add(item);

                    crawlStatic.Add(item.staticFieldsArrayIndex);
                }

                //if (hasStaticField)
                    staticManagedTypes.Add(type.managedTypesArrayIndex);
            }

            m_Snapshot.managedStaticTypes = staticManagedTypes.ToArray();

            //var loopGuard = 0;
            while (crawlStatic.Count > 0)
            {
                //if (++loopGuard > 100000)
                //{
                //    m_snapshot.Error("Loop-guard kicked in while analyzing static fields.");
                //    break;
                //}

                m_TotalCrawled++;
                if ((m_TotalCrawled % 1000) == 0)
                {
                    UpdateProgress();
                    if (m_Snapshot.abortActiveStepRequested)
                        break;
                }

                var staticField = m_StaticFields[crawlStatic[crawlStatic.Count - 1]];
                crawlStatic.RemoveAt(crawlStatic.Count - 1);

                var staticClass = m_Snapshot.managedTypes[staticField.managedTypesArrayIndex];
                var field = staticClass.fields[staticField.fieldIndex];
                var fieldType = m_Snapshot.managedTypes[field.managedTypesArrayIndex];
                var staticReader = new StaticMemoryReader(m_Snapshot, staticClass.staticFieldBytes);

                if (fieldType.isValueType)
                {
                    if (staticClass.staticFieldBytes == null || staticClass.staticFieldBytes.Length == 0)
                        continue;

                    var newObj = PackedManagedObject.New();
                    newObj.address = (ulong)field.offset;
                    // BUG: TODO: If staticFieldsArrayIndex=0, then it's detected as managedObject rather than staticField?
                    newObj.managedObjectsArrayIndex = -staticField.staticFieldsArrayIndex;
                    newObj.managedTypesArrayIndex = fieldType.managedTypesArrayIndex;
                    newObj.staticBytes = staticClass.staticFieldBytes;
                    SetObjectSize(ref newObj, fieldType);
                    
                    m_Crawl.Add(newObj);
                    m_TotalCrawled++;
                    continue;
                }

                // If it's a reference type, it simply points to a ManagedObject on the heap and all
                // we need to do it to create a new ManagedObject and add it to the list to crawl.
                if (!fieldType.isValueType)
                {
                    var addr = staticReader.ReadPointer((uint)field.offset);
                    if (addr == 0)
                        continue;

#if DEBUG_BREAK_ON_ADDRESS
                    if (addr == DebugBreakOnAddress)
                    {
                        int a = 0;
                    }
#endif

                    int newObjIndex;
                    if (!m_Seen.TryGetValue(addr, out newObjIndex))
                    {
                        var newObj = PackedManagedObject.New();
                        newObj.address = addr;
                        newObj.managedObjectsArrayIndex = m_ManagedObjects.Count;

                        // The static field could be a basetype, such as UnityEngine.Object, but actually point to a Texture2D.
                        // Therefore it's important to find the type of the specified address, rather than using the field type.
                        newObj.managedTypesArrayIndex = m_Snapshot.FindManagedObjectTypeOfAddress(addr);
                        if (newObj.managedTypesArrayIndex == -1)
                            newObj.managedTypesArrayIndex = fieldType.managedTypesArrayIndex;

                        // Check if the object has a GCHandle
                        var gcHandleIndex = m_Snapshot.FindGCHandleOfTargetAddress(addr);
                        if (gcHandleIndex != -1)
                        {
                            newObj.gcHandlesArrayIndex = gcHandleIndex;
                            m_Snapshot.gcHandles[gcHandleIndex].managedObjectsArrayIndex = newObj.managedObjectsArrayIndex;

                            m_Snapshot.AddConnection(PackedConnection.Kind.GCHandle, gcHandleIndex, PackedConnection.Kind.Managed, newObj.managedObjectsArrayIndex);
                        }
                        SetObjectSize(ref newObj, managedTypes[newObj.managedTypesArrayIndex]);
                        TryConnectNativeObject(ref newObj);

                        m_ManagedObjects.Add(newObj);
                        m_Seen[newObj.address] = newObj.managedObjectsArrayIndex;
                        m_Crawl.Add(newObj);
                        m_TotalCrawled++;
                        newObjIndex = newObj.managedObjectsArrayIndex;
                    }

                    m_Snapshot.AddConnection(PackedConnection.Kind.StaticField, staticField.staticFieldsArrayIndex, PackedConnection.Kind.Managed, newObjIndex);

                    continue;
                }

            }
        }

        int TryAddManagedObject(ulong address)
        {
            // Try to find the ManagedObject of the current GCHandle
            var typeIndex = m_Snapshot.FindManagedObjectTypeOfAddress(address);
            if (typeIndex == -1)
            {
                #region Unity Bug
                // Unity BUG: (Case 977003) PackedMemorySnapshot: Unable to resolve typeDescription of GCHandle.target
                // https://issuetracker.unity3d.com/issues/packedmemorysnapshot-unable-to-resolve-typedescription-of-gchandle-dot-target
                // [quote=Unity]
                // This is a bug in Mono where it has a few GC handles that point to invalid objects and they should
                // removed from the list of GC handles. The the invalid GC handles can be ignored for now,
                // as they have no affect on the captured snapshot.
                // [/quote]
                #endregion
                m_Snapshot.Warning("HeapExplorer: Cannot find GCHandle target '{0:X}' (Unity bug Case 977003).", address);
                return -1;
            }

            var managedObj = new PackedManagedObject
            {
                address = address,
                managedTypesArrayIndex = typeIndex,
                managedObjectsArrayIndex = m_ManagedObjects.Count,
                gcHandlesArrayIndex = -1,
                nativeObjectsArrayIndex = -1
            };
            
            // If the ManagedObject is the representation of a NativeObject, connect the two
            TryConnectNativeObject(ref managedObj);
            SetObjectSize(ref managedObj, m_Snapshot.managedTypes[managedObj.managedTypesArrayIndex]);

            m_Seen[managedObj.address] = managedObj.managedObjectsArrayIndex;
            m_ManagedObjects.Add(managedObj);
            m_Crawl.Add(managedObj);

            return managedObj.managedObjectsArrayIndex;
        }

        void CrawlGCHandles()
        {
            var gcHandles = m_Snapshot.gcHandles;

            for (int n=0, nend = gcHandles.Length; n < nend; ++n)
            {
                if (gcHandles[n].target == 0)
                {
                    m_Snapshot.Warning("HeapExplorer: Cannot find GCHandle target '{0:X}' (Unity bug Case 977003).", gcHandles[n].target);
                    continue;
                }

#if DEBUG_BREAK_ON_ADDRESS
                if (gcHandles[n].target == DebugBreakOnAddress)
                {
                    int a = 0;
                }
#endif

                var managedObjectIndex = TryAddManagedObject(gcHandles[n].target);
                if (managedObjectIndex == -1)
                    continue;

                var managedObj = m_ManagedObjects[managedObjectIndex];
                managedObj.gcHandlesArrayIndex = gcHandles[n].gcHandlesArrayIndex;
                m_ManagedObjects[managedObjectIndex] = managedObj;

                // Connect GCHandle to ManagedObject
                m_Snapshot.AddConnection(PackedConnection.Kind.GCHandle, gcHandles[n].gcHandlesArrayIndex, PackedConnection.Kind.Managed, managedObj.managedObjectsArrayIndex);

                // Update the GCHandle with the index to its managed object
                gcHandles[n].managedObjectsArrayIndex = managedObj.managedObjectsArrayIndex;

                m_TotalCrawled++;

                if ((m_TotalCrawled % 1000) == 0)
                    UpdateProgress();
            }
        }

        void UpdateProgress()
        {
            m_Snapshot.busyString = string.Format("Analyzing Managed Objects\n{0} crawled, {1} extracted", m_TotalCrawled, m_ManagedObjects.Count);
        }

        void SetObjectSize(ref PackedManagedObject managedObj, PackedManagedType type)
        {
            if (managedObj.size > 0)
                return; // size is set already 

            managedObj.size = m_MemoryReader.ReadObjectSize(managedObj.address, type);
        }

        void TryConnectNativeObject(ref PackedManagedObject managedObj)
        {
            if (managedObj.nativeObjectsArrayIndex >= 0)
                return; // connected already

            // If it's not derived from UnityEngine.Object, it does not have the m_CachedPtr field and we can skip it
            var type = m_Snapshot.managedTypes[managedObj.managedTypesArrayIndex];
            if (type.isValueType || type.isArray)
                return;

            // Only types derived from UnityEngine.Object have the m_cachePtr field
            if (!type.isUnityEngineObject)
                return;

            BeginProfilerSample("ReadPointer");
            // Read the m_cachePtr value
            var nativeObjectAddress = m_MemoryReader.ReadPointer(managedObj.address + (uint)m_CachedPtr.offset);
            EndProfilerSample();
            if (nativeObjectAddress == 0)
                return;

            // Try to find the corresponding native object
            BeginProfilerSample("FindNativeObjectOfAddress");
            var nativeObjectArrayIndex = m_Snapshot.FindNativeObjectOfAddress(nativeObjectAddress);
            EndProfilerSample();
            if (nativeObjectArrayIndex < 0)
                return;

            // Connect ManagedObject <> NativeObject
            managedObj.nativeObjectsArrayIndex = nativeObjectArrayIndex;
            m_Snapshot.nativeObjects[managedObj.nativeObjectsArrayIndex].managedObjectsArrayIndex = managedObj.managedObjectsArrayIndex;

            // Connect the ManagedType <> NativeType
            m_Snapshot.managedTypes[managedObj.managedTypesArrayIndex].nativeTypeArrayIndex = m_Snapshot.nativeObjects[managedObj.nativeObjectsArrayIndex].nativeTypesArrayIndex;
            m_Snapshot.nativeTypes[m_Snapshot.nativeObjects[managedObj.nativeObjectsArrayIndex].nativeTypesArrayIndex].managedTypeArrayIndex = m_Snapshot.managedTypes[managedObj.managedTypesArrayIndex].managedTypesArrayIndex;

            BeginProfilerSample("AddConnection");
            // Add a Connection from ManagedObject to NativeObject (m_CachePtr)
            m_Snapshot.AddConnection(PackedConnection.Kind.Managed, managedObj.managedObjectsArrayIndex, PackedConnection.Kind.Native, managedObj.nativeObjectsArrayIndex);
            EndProfilerSample();
        }
    }
}
