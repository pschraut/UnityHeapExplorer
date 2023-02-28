//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//

//#define DEBUG_BREAK_ON_ADDRESS
//#define ENABLE_PROFILING
//#define ENABLE_PROFILER
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Collections.Concurrent;
using HeapExplorer.Utilities;
using static HeapExplorer.Utilities.Option;

namespace HeapExplorer
{
    public class PackedManagedObjectCrawler
    {
        long m_TotalCrawled;

        readonly struct PendingObject {
            /// <summary>Object that we need to crawl.</summary>
            public readonly PackedManagedObject obj;

            /// <summary>
            /// The field that has the reference to the <see cref="obj"/>. This can be `None` if the object is
            /// referenced from the native side. 
            /// </summary>
            public readonly Option<PackedManagedField> sourceField;

            public PendingObject(PackedManagedObject obj, Option<PackedManagedField> sourceField) {
                this.obj = obj;
                this.sourceField = sourceField;
            }
        }
        
        /// <summary>Stack of objects that still need crawling.</summary>
        readonly Stack<PendingObject> m_Crawl = new Stack<PendingObject>(1024 * 1024);
        
        readonly Dictionary<ulong, PackedManagedObject.ArrayIndex> m_Seen = 
            new Dictionary<ulong, PackedManagedObject.ArrayIndex>(1024 * 1024);
        
        PackedMemorySnapshot m_Snapshot;
        PackedManagedField m_CachedPtr;
        MemoryReader m_MemoryReader;

#if DEBUG_BREAK_ON_ADDRESS
        /// <summary>
        /// Allows you to write an address here and then put breakpoints at everywhere where this value is used to
        /// quickly break in debugger when we're dealing with an object with this particular memory address.
        /// </summary>
        const ulong DebugBreakOnAddress = 0x20C98ECE000;
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

        public void Crawl(PackedMemorySnapshot snapshot, List<ulong> substituteAddresses)
        {
            m_TotalCrawled = 0;
            m_Snapshot = snapshot;
            m_MemoryReader = new MemoryReader(m_Snapshot);
            InitializeCachedPtr();

            var managedObjects = new List<PackedManagedObject>(1024 * 1024);

            BeginProfilerSample("CrawlGCHandles");
            CrawlGCHandles(managedObjects);
            EndProfilerSample();

            BeginProfilerSample("substituteAddresses");
            for (var n = 0; n < substituteAddresses.Count; ++n)
            {
                var addr = substituteAddresses[n];
                if (m_Seen.ContainsKey(addr))
                    continue;
                TryAddManagedObject(addr, managedObjects);
            }
            EndProfilerSample();
            
            BeginProfilerSample("CrawlStatic");
            (m_Snapshot.managedStaticFields, m_Snapshot.managedStaticTypes) = CrawlStatic(managedObjects);
            EndProfilerSample();

            BeginProfilerSample("CrawlManagedObjects");
            CrawlManagedObjects(managedObjects);
            m_Snapshot.managedObjects = managedObjects.ToArray();
            UpdateProgress(managedObjects);
            EndProfilerSample();
        }

        void InitializeCachedPtr()
        {
            var unityEngineObject = m_Snapshot.managedTypes[m_Snapshot.coreTypes.unityEngineObject];

            // UnityEngine.Object types on the managed side have a m_CachedPtr field that
            // holds the native memory address of the corresponding native object of this managed object.
            const string FIELD_NAME = "m_CachedPtr";

            for (int n = 0, nend = unityEngineObject.fields.Length; n < nend; ++n)
            {
                var field = unityEngineObject.fields[n];
                if (field.name != FIELD_NAME)
                    continue;

                m_CachedPtr = field;
                return;
            }
            
            throw new Exception(
                $"HeapExplorer: Could not find '{FIELD_NAME}' field for Unity object type '{unityEngineObject.name}', "
                + "this probably means the internal structure of Unity has changed and the tool needs updating."
            );
        }

        void CrawlManagedObjects(
            List<PackedManagedObject> managedObjects
        ) {
            var virtualMachineInformation = m_Snapshot.virtualMachineInformation;
            var nestedStructsIgnored = 0;

            var seenBaseTypes = new CycleTracker<int>();
            //var guard = 0;
            while (m_Crawl.Count > 0) {
                //if (++guard > 10000000)
                //{
                //    Debug.LogWarning("Loop guard kicked in");
                //    break;
                //}
                if (m_TotalCrawled % 1000 == 0) {
                    UpdateProgress(managedObjects);
                    if (m_Snapshot.abortActiveStepRequested) break;
                }

                // Take an object that we want to crawl.
                var obj = m_Crawl.Pop();

#if DEBUG_BREAK_ON_ADDRESS
                if (obj.obj.address == DebugBreakOnAddress) {
                    int a = 0;
                }
#endif

                seenBaseTypes.markStartOfSearch();
                
                // Go through the type hierarchy, down to the base type.
                var maybeTypeIndex = Some(obj.obj.managedTypesArrayIndex);
                {while (maybeTypeIndex.valueOut(out var typeIndex)) {
                    if (seenBaseTypes.markIteration(typeIndex)) {
                        seenBaseTypes.reportCycle(
                            $"{nameof(CrawlManagedObjects)}()", typeIndex,
                            idx => m_Snapshot.managedTypes[idx].ToString()
                        );
                        break;
                    }

                    var type = m_Snapshot.managedTypes[typeIndex];
                    AbstractMemoryReader memoryReader = m_MemoryReader;
                    {if (obj.obj.staticBytes.valueOut(out var staticBytes))
                        memoryReader = new StaticMemoryReader(m_Snapshot, staticBytes);}

                    {if (type.arrayRank.valueOut(out var arrayRank)) {
                        handleArrayType(obj, type, arrayRank, memoryReader);
                        break;
                    } else {
                        var shouldBreak = handleNonArrayType(obj.obj, type, memoryReader, typeIndex: typeIndex);
                        if (shouldBreak) break;
                        else maybeTypeIndex = type.baseOrElementTypeIndex;
                    }}
                }}
            }

            if (nestedStructsIgnored > 0) m_Snapshot.Warning(
                $"HeapExplorer: {nestedStructsIgnored} nested structs ignored (Workaround for Unity bug Case 1104590)."
            );
            
            void handleArrayType(
                PendingObject pendingObject, PackedManagedType type, PInt arrayRank, AbstractMemoryReader memoryReader
            ) {
                var mo = pendingObject.obj;
                if (
                    !type.baseOrElementTypeIndex.valueOut(out var baseOrElementTypeIndex) 
                    || baseOrElementTypeIndex >= m_Snapshot.managedTypes.Length
                ) {
                    m_Snapshot.Error(
                        $"HeapExplorer: '{type.name}.baseOrElementTypeIndex' = {baseOrElementTypeIndex} at address "
                        + $"'{mo.address:X}', ignoring managed object."
                    );
                    return;
                }

                var elementType = m_Snapshot.managedTypes[baseOrElementTypeIndex];
                if (elementType.isValueType && elementType.isPrimitive)
                    return; // don't crawl int[], byte[], etc

                // If the value type does not contain any reference types nested in it there is no point in analysing
                // the memory further.
                if (elementType.isValueType && !m_Snapshot.isOrContainsReferenceType(elementType.managedTypesArrayIndex))
                    return;

                int dim0Length;
                if (mo.address > 0) {
                    if (!memoryReader.ReadArrayLength(mo.address, arrayRank).valueOut(out dim0Length)) {
                        m_Snapshot.Error($"Can't determine array length for array at {mo.address:X}");
                        return;
                    }
                }
                else
                    dim0Length = 0;
                
                //if (dim0Length > 1024 * 1024)
                if (dim0Length > (32*1024) * (32*1024)) {
                    m_Snapshot.Error(
                        $"HeapExplorer: Array (rank={arrayRank}) found at address '{mo.address:X} with "
                        + $"'{dim0Length}' elements, that doesn't seem right."
                    );
                    return;
                }

                for (var k = 0; k < dim0Length; ++k) {
                    if (m_TotalCrawled % 1000 == 0)
                        UpdateProgress(managedObjects);

                    if (!determineElementAddress().valueOut(out var elementAddr)) continue;

#if DEBUG_BREAK_ON_ADDRESS
                    if (elementAddr == DebugBreakOnAddress) {
                        int a = 0;
                    }
#endif

                    // Skip null references.
                    if (elementAddr == 0) continue;
                    
                    if (!m_Seen.TryGetValue(elementAddr, out var newObjectIndex)) {
                        var managedTypesArrayIndex = elementType.managedTypesArrayIndex;
                        var managedObjectsArrayIndex = 
                            PackedManagedObject.ArrayIndex.newObject(managedObjects.CountP());
                        var newObj = PackedManagedObject.New(
                            address: elementAddr,
                            managedTypesArrayIndex: managedTypesArrayIndex,
                            managedObjectsArrayIndex: managedObjectsArrayIndex
                        );

                        if (elementType.isValueType) {
                            newObj.managedObjectsArrayIndex = mo.managedObjectsArrayIndex;
                            newObj.staticBytes = mo.staticBytes;
                        }
                        else {
                            newObj.managedTypesArrayIndex = 
                                m_Snapshot.FindManagedObjectTypeOfAddress(elementAddr)
                                    .getOrElse(elementType.managedTypesArrayIndex);

                            TryConnectNativeObject(ref newObj);
                        }
                        SetObjectSize(ref newObj, m_Snapshot.managedTypes[newObj.managedTypesArrayIndex]);

                        if (elementType.isReferenceType)
                            managedObjects.Add(newObj);

                        m_Seen[newObj.address] = newObj.managedObjectsArrayIndex;
                        m_Crawl.Push(new PendingObject(newObj, pendingObject.sourceField));
                        m_TotalCrawled++;
                        newObjectIndex = newObj.managedObjectsArrayIndex;
                    }

                    if (elementType.isReferenceType) {
                        m_Snapshot.AddConnection(
                            new PackedConnection.From(
                                mo.managedObjectsArrayIndex.asPair,
                                // Artūras Šlajus: I am not sure how to get the referencing field here.
                                // TODO: fixme
                                field: None._
                            ),
                            newObjectIndex.asPair
                        );
                    }
                    
                    // Determines the memory address for the `k`th element.
                    Option<ulong> determineElementAddress() {
                        // Artūras Šlajus: Not sure why these checks are done in this order but I am too scared to
                        // switch the order.
                        if (elementType.isArray) return readPtr();
                        if (elementType.isValueType) {
                            if (elementType.size.valueOut(out var elementTypeSize))
                                return Some(
                                    mo.address
                                    + (ulong) (k * elementTypeSize)
                                    + virtualMachineInformation.arrayHeaderSize
                                    - virtualMachineInformation.objectHeaderSize
                                );
                            else {
                                Utils.reportInvalidSizeError(elementType, m_Snapshot.reportedErrors);
                                return None._;
                            }
                        }
                        else return readPtr();

                        Option<ulong> readPtr() {
                            var ptr = mo.address
                                      + (ulong) (k * virtualMachineInformation.pointerSize.sizeInBytes())
                                      + virtualMachineInformation.arrayHeaderSize;
                            var maybeAddress = memoryReader.ReadPointer(ptr);
                            if (maybeAddress.isNone) {
                                m_Snapshot.Error($"HeapExplorer: Can't read ptr={ptr:X} for k={k}, type='{elementType.name}'");
                            }

                            return maybeAddress;
                        }
                    }
                }
            }

            // Returns true
            bool handleNonArrayType(
                PackedManagedObject obj, PackedManagedType type, AbstractMemoryReader memoryReader, PInt typeIndex
            ) {
                // Go through the fields in the type.
                for (var n = 0; n < type.fields.Length; ++n) {
                    var field = type.fields[n];
                    
                    // Skip static fields as they are not a part of the object.
                    if (field.isStatic)
                        continue;

                    var fieldType = m_Snapshot.managedTypes[field.managedTypesArrayIndex];

                    if (fieldType.isValueType) handleValueTypeField(fieldType, field);
                    else handleReferenceTypeField(fieldType, field);
                }

                return Some(typeIndex) == type.baseOrElementTypeIndex || type.isArray;

                void handleValueTypeField(PackedManagedType fieldType, PackedManagedField field) {
                    // Primitive values types do not contain any references that we would care about. 
                    if (fieldType.isPrimitive)
                        return;

                    // This shouldn't be possible, you can't put a value type into itself, as it would have
                    // infinite size. But you know, things happen...
                    var isNestedStruct = obj.managedTypesArrayIndex == fieldType.managedTypesArrayIndex;
                    if (isNestedStruct) {
                        nestedStructsIgnored++;
                        return;
                    }

                    // If this type contains reference types, we need to crawl it further. However, we do not add value
                    // types to the `managedObjects` list.
                    if (m_Snapshot.isOrContainsReferenceType(fieldType.managedTypesArrayIndex)) {
                        var address = obj.address + field.offset - virtualMachineInformation.objectHeaderSize;
                        var newObj = PackedManagedObject.New(
                            address: address,
                            managedTypesArrayIndex: fieldType.managedTypesArrayIndex,
                            managedObjectsArrayIndex: obj.managedObjectsArrayIndex,
                            size: GetObjectSize(address, fieldType),
                            staticBytes: obj.staticBytes
                        );
                        m_Crawl.Push(new PendingObject(newObj, Some(field))); 
                    }

                    m_TotalCrawled++;
                }

                void handleReferenceTypeField(PackedManagedType fieldType, PackedManagedField field) {
                    var ptr =
                        obj.staticBytes.isNone
                        ? obj.address + (uint)field.offset
                        : obj.address + (uint)field.offset - (uint)virtualMachineInformation.objectHeaderSize;

                    if (!memoryReader.ReadPointer(ptr).valueOut(out var addr)) {
                        Debug.LogError($"HeapExplorer: Can't read ptr={ptr:X} for fieldType='{fieldType.name}'");
                        return;
                    }

                    // Ignore null pointers.
                    if (addr == 0)
                        return;

#if DEBUG_BREAK_ON_ADDRESS
                    if (addr == DebugBreakOnAddress) {
                        int a = 0;
                    }
#endif

                    if (!m_Seen.TryGetValue(addr, out var newObjIndex)) {
                        var maybeManagedTypesArrayIndex = m_Snapshot.FindManagedObjectTypeOfAddress(addr);
                        var managedTypesArrayIndex =
                            maybeManagedTypesArrayIndex.getOrElse(fieldType.managedTypesArrayIndex);
                        
                        var newObj = PackedManagedObject.New(
                            address: addr,
                            managedObjectsArrayIndex: PackedManagedObject.ArrayIndex.newObject(managedObjects.CountP()),
                            managedTypesArrayIndex: managedTypesArrayIndex,
                            size: GetObjectSize(addr, m_Snapshot.managedTypes[managedTypesArrayIndex])
                        );

                        TryConnectNativeObject(ref newObj);

                        m_Seen[newObj.address] = newObj.managedObjectsArrayIndex;
                        managedObjects.Add(newObj);
                        m_Crawl.Push(new PendingObject(newObj, Some(field)));
                        m_TotalCrawled++;
                        
                        newObjIndex = newObj.managedObjectsArrayIndex;
                    }

                    m_Snapshot.AddConnection(
                        new PackedConnection.From(obj.managedObjectsArrayIndex.asPair, Some(field)),
                        newObjIndex.asPair
                    );
                }
            }
        }

        (PackedManagedStaticField[] staticFields, int[] managedStaticTypes) CrawlStatic(
            List<PackedManagedObject> managedObjects
        ) {
            var crawlStatic = new Stack<int>();
            var managedTypes = m_Snapshot.managedTypes;
            var staticFields = new List<PackedManagedStaticField>(10 * 1024);
            var staticManagedTypes = new List<int>(1024);
            collectStaticFields();

            void collectStaticFields() {
                // Unity BUG: (Case 984330) PackedMemorySnapshot: Type contains staticFieldBytes, but has no static fields
                for (int n = 0, nend = managedTypes.Length; n < nend; ++n) {
                    // Some static classes have no staticFieldBytes. As I understand this, the staticFieldBytes
                    // are only filled if that static class has been initialized (its cctor called), otherwise it's zero.
                    //
                    // This is normal behaviour.
                    if (managedTypes[n].staticFieldBytes == null || managedTypes[n].staticFieldBytes.Length == 0) {
                        // Debug.LogFormat(
                        //     "HeapExplorer: managed type '{0}' does not have static fields.", managedTypes[n].name
                        // );
                        continue;
                    }

                    //var hasStaticField = false;
                    for (
                        PInt fieldIndex = PInt._0, fieldIndexEnd = managedTypes[n].fields.LengthP(); 
                        fieldIndex < fieldIndexEnd;
                        ++fieldIndex
                    ) {
                        if (!managedTypes[n].fields[fieldIndex].isStatic)
                            continue;

                        //var field = managedTypes[n].fields[j];
                        //var fieldType = managedTypes[field.managedTypesArrayIndex];
                        //hasStaticField = true;

                        var item = new PackedManagedStaticField(
                            managedTypesArrayIndex: managedTypes[n].managedTypesArrayIndex,
                            fieldIndex: fieldIndex,
                            staticFieldsArrayIndex: staticFields.CountP()
                        );
                        staticFields.Add(item);

                        crawlStatic.Push(item.staticFieldsArrayIndex);
                    }

                    //if (hasStaticField)
                    staticManagedTypes.Add(managedTypes[n].managedTypesArrayIndex);
                }
            }
 
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
                    UpdateProgress(managedObjects);
                    if (m_Snapshot.abortActiveStepRequested)
                        break;
                }

                var staticFieldIndex = crawlStatic.Pop();
                var staticField = staticFields[staticFieldIndex];

                var staticClass = m_Snapshot.managedTypes[staticField.managedTypesArrayIndex];
                var field = staticClass.fields[staticField.fieldIndex];
                var fieldType = m_Snapshot.managedTypes[field.managedTypesArrayIndex];
                var staticReader = new StaticMemoryReader(m_Snapshot, staticClass.staticFieldBytes);

                if (fieldType.isValueType)
                {
                    if (staticClass.staticFieldBytes == null || staticClass.staticFieldBytes.Length == 0)
                        continue;

                    var newObj = PackedManagedObject.New(
                        address: field.offset,
                        PackedManagedObject.ArrayIndex.newStatic(staticField.staticFieldsArrayIndex),
                        managedTypesArrayIndex: fieldType.managedTypesArrayIndex 
                    );
                    newObj.staticBytes = Some(staticClass.staticFieldBytes);
                    SetObjectSize(ref newObj, fieldType);

                    m_Crawl.Push(new PendingObject(newObj, Some(field)));
                    m_TotalCrawled++;
                }
                // If it's a reference type, it simply points to a ManagedObject on the heap and all
                // we need to do it to create a new ManagedObject and add it to the list to crawl.
                else
                {
                    if (!staticReader.ReadPointer((uint) field.offset).valueOut(out var addr)) {
                        m_Snapshot.Error($"Can't do `staticReader.ReadPointer(field.offset={field.offset})`");
                        continue;
                    }
                    if (addr == 0)
                        continue;

#if DEBUG_BREAK_ON_ADDRESS
                    if (addr == DebugBreakOnAddress) {
                        int a = 0;
                    }
#endif

                    if (!m_Seen.TryGetValue(addr, out var newObjIndex))
                    {
                        // The static field could be a basetype, such as `UnityEngine.Object`, but actually point to a `Texture2D`.
                        // Therefore it's important to find the type of the specified address, rather than using the field type.
                        var maybeManagedTypesArrayIndex = m_Snapshot.FindManagedObjectTypeOfAddress(addr);
                        var managedTypesArrayIndex = 
                            maybeManagedTypesArrayIndex.getOrElse(fieldType.managedTypesArrayIndex);

                        var newObj = PackedManagedObject.New(
                            address: addr,
                            managedObjectsArrayIndex: PackedManagedObject.ArrayIndex.newObject(
                                managedObjects.CountP()
                            ),
                            managedTypesArrayIndex: managedTypesArrayIndex
                        );

                        // Check if the object has a GCHandle
                        var maybeGcHandleIndex = m_Snapshot.FindGCHandleOfTargetAddress(addr);
                        {if (maybeGcHandleIndex.valueOut(out var gcHandleIndex)) {
                            newObj.gcHandlesArrayIndex = Some(gcHandleIndex);
                            m_Snapshot.gcHandles[gcHandleIndex] =
                                m_Snapshot.gcHandles[gcHandleIndex]
                                .withManagedObjectsArrayIndex(newObj.managedObjectsArrayIndex);

                            m_Snapshot.AddConnection(
                                new PackedConnection.From(
                                    new PackedConnection.Pair(PackedConnection.Kind.GCHandle, gcHandleIndex),
                                    field: None._
                                ),
                                newObj.managedObjectsArrayIndex.asPair
                            );
                        }}
                        SetObjectSize(ref newObj, managedTypes[newObj.managedTypesArrayIndex]);
                        TryConnectNativeObject(ref newObj);

                        managedObjects.Add(newObj);
                        m_Seen[newObj.address] = newObj.managedObjectsArrayIndex;
                        m_Crawl.Push(new PendingObject(newObj, Some(field)));
                        m_TotalCrawled++;
                        newObjIndex = newObj.managedObjectsArrayIndex;
                    }

                    m_Snapshot.AddConnection(
                        new PackedConnection.From(
                            new PackedConnection.Pair(PackedConnection.Kind.StaticField, staticField.staticFieldsArrayIndex),
                            Some(field)
                        ),
                        newObjIndex.asPair
                    );
                }
            }

            return (staticFields.ToArray(), staticManagedTypes.ToArray());
        }

        /// <summary>
        /// Creates and stores a <see cref="PackedManagedObject"/> in <see cref="m_ManagedObjects"/>.
        /// </summary>
        /// <param name="address"></param>
        /// <returns>The index into <see cref="m_ManagedObjects"/> array.</returns>
        Option<PInt> TryAddManagedObject(ulong address, List<PackedManagedObject> managedObjects)
        {
            // Try to find the ManagedObject of the current GCHandle
            var maybeTypeIndex = m_Snapshot.FindManagedObjectTypeOfAddress(address);
            if (!maybeTypeIndex.valueOut(out var typeIndex))
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
                return None._;
            }

            var index = managedObjects.CountP();
            var managedObj = PackedManagedObject.New(
                address: address,
                managedTypesArrayIndex: typeIndex,
                managedObjectsArrayIndex: PackedManagedObject.ArrayIndex.newObject(index)
            );

            // If the ManagedObject is the representation of a NativeObject, connect the two
            TryConnectNativeObject(ref managedObj);
            SetObjectSize(ref managedObj, m_Snapshot.managedTypes[managedObj.managedTypesArrayIndex]);

            m_Seen[managedObj.address] = managedObj.managedObjectsArrayIndex;
            managedObjects.Add(managedObj);
            m_Crawl.Push(new PendingObject(managedObj, sourceField: None._));

            return Some(index);
        }

        void CrawlGCHandles(List<PackedManagedObject> managedObjects)
        {
            var gcHandles = m_Snapshot.gcHandles;

            for (int n=0, nend = gcHandles.Length; n < nend; ++n) 
            {
                var gcHandle = gcHandles[n];
                if (gcHandle.target == 0)
                {
                    m_Snapshot.Warning("HeapExplorer: Cannot find GCHandle target '{0:X}' (Unity bug Case 977003).", gcHandle.target);
                    continue;
                }

#if DEBUG_BREAK_ON_ADDRESS
                if (gcHandle.target == DebugBreakOnAddress) {
                    int a = 0;
                }
#endif

                var maybeManagedObjectIndex = TryAddManagedObject(gcHandle.target, managedObjects);
                if (!maybeManagedObjectIndex.valueOut(out var managedObjectIndex)) {
                    Debug.LogWarning($"HeapExplorer: Can't find managed object for GC handle {gcHandle.target:X}, skipping!");
                    continue;
                }

                var managedObj = managedObjects[managedObjectIndex];
                managedObj.gcHandlesArrayIndex = Some(gcHandle.gcHandlesArrayIndex);
                managedObjects[managedObjectIndex] = managedObj;

                // Connect GCHandle to ManagedObject
                m_Snapshot.AddConnection(
                    new PackedConnection.From(
                        new PackedConnection.Pair(PackedConnection.Kind.GCHandle, gcHandle.gcHandlesArrayIndex),
                        field: None._
                    ),
                    managedObj.managedObjectsArrayIndex.asPair
                );

                // Update the GCHandle with the index to its managed object
                gcHandle = gcHandle.withManagedObjectsArrayIndex(managedObj.managedObjectsArrayIndex);
                gcHandles[n] = gcHandle;

                m_TotalCrawled++;

                if ((m_TotalCrawled % 1000) == 0)
                    UpdateProgress(managedObjects);
            }
        }

        void UpdateProgress(List<PackedManagedObject> managedObjects)
        {
            m_Snapshot.busyString =
                $"Analyzing Managed Objects\n{m_TotalCrawled} crawled, {managedObjects.Count} extracted";
        }

        void SetObjectSize(ref PackedManagedObject managedObj, PackedManagedType type)
        {
            if (managedObj.size.isSome)
                return; // size is set already

            managedObj.size = GetObjectSize(managedObj.address, type);
        }

        Option<uint> GetObjectSize(ulong address, PackedManagedType type) {
            var maybeSize = m_MemoryReader.ReadObjectSize(address, type);
            if (maybeSize.isNone) {
                Debug.LogError($"HeapExplorer: Can't read object size for managed object of type '{type.name}' at 0x{address:X}");
            }
            return maybeSize.map(size => size.asUInt);
        }

        void TryConnectNativeObject(ref PackedManagedObject managedObj)
        {
            if (managedObj.nativeObjectsArrayIndex.isSome)
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
            var nativeObjectAddressPtr = managedObj.address + (uint) m_CachedPtr.offset;
            if (
                !m_MemoryReader.ReadPointer(nativeObjectAddressPtr).valueOut(out var nativeObjectAddress)
            ) {
                Debug.LogError(
                    $"HeapExplorer: Can't read {nameof(m_CachedPtr)} from a managed object at ptr={nativeObjectAddressPtr:X}"
                );
                return;
            }
            EndProfilerSample();
            // If the native object address is 0 then we have a managed object without the native side, which happens
            // when you have a leaked managed object.
            if (nativeObjectAddress == 0)
                return;

            // Try to find the corresponding native object
            BeginProfilerSample("FindNativeObjectOfAddress");
            var maybeNativeObjectArrayIndex = m_Snapshot.FindNativeObjectOfAddress(nativeObjectAddress);
            EndProfilerSample();
            {if (maybeNativeObjectArrayIndex.valueOut(out var nativeObjectArrayIndex)) {
                // Connect ManagedObject <> NativeObject
                managedObj.nativeObjectsArrayIndex = Some(nativeObjectArrayIndex);
                m_Snapshot.nativeObjects[nativeObjectArrayIndex].managedObjectsArrayIndex = Some(managedObj.managedObjectsArrayIndex);

                // Connect the ManagedType <> NativeType
                var nativeTypesArrayIndex = m_Snapshot.nativeObjects[nativeObjectArrayIndex].nativeTypesArrayIndex;
                m_Snapshot.managedTypes[managedObj.managedTypesArrayIndex].nativeTypeArrayIndex = 
                    Some(nativeTypesArrayIndex);
                m_Snapshot.nativeTypes[nativeTypesArrayIndex].managedTypeArrayIndex = 
                    Some(m_Snapshot.managedTypes[managedObj.managedTypesArrayIndex].managedTypesArrayIndex);
                
                BeginProfilerSample("AddConnection");
                // Add a Connection from ManagedObject to NativeObject (m_CachePtr)
                m_Snapshot.AddConnection(
                    new PackedConnection.From(
                        managedObj.managedObjectsArrayIndex.asPair,
                        Some(m_CachedPtr)
                    ),
                    new PackedConnection.Pair(PackedConnection.Kind.Native, nativeObjectArrayIndex)
                );
                EndProfilerSample();
            }}
        }
    }
}
