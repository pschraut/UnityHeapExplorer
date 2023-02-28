//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using HeapExplorer.Utilities;
using UnityEditor.Profiling.Memory.Experimental;
using static HeapExplorer.Utilities.Option;

namespace HeapExplorer
{
    /// <summary>
    /// Description of a managed type.
    /// </summary>
    /// <note>
    /// This needs to be a class, not a struct because we cache <see cref="instanceFields"/> and
    /// <see cref="staticFields"/>.
    /// </note>
    [Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    public class PackedManagedType : PackedMemorySnapshot.TypeForSubclassSearch
    {
        /// <summary>Is this type a value type? (if it's not a value type, it's a reference type)</summary>
        public readonly bool isValueType;

        /// <summary>Is this type a reference type? (if it's not a reference type, it's a value type)</summary>
        public bool isReferenceType => !isValueType;

        /// <summary>
        /// `None` if this type is not an array, `Some(arrayRank)` if it is an array.
        /// <para/>
        /// The rank of the array is 1 for a 1-dimensional array, 2 for a 2-dimensional array, etc.
        /// </summary>
        public readonly Option<PInt> arrayRank;

        public bool isArray => arrayRank.isSome;

        /// <summary>
        /// Name of this type.
        /// </summary>
        public readonly string name;

        /// <summary>
        /// Name of the assembly this type was loaded from.
        /// </summary>
        public readonly string assembly;

        /// <summary>
        /// An array containing descriptions of all fields of this type.
        /// </summary>
        public readonly PackedManagedField[] fields;

        /// <summary>
        /// The actual contents of the bytes that store this types static fields, at the point of time when the
        /// snapshot was taken.
        /// </summary>
        public readonly byte[] staticFieldBytes;

        /// <summary>
        /// The base or element type for this type, pointed to by an index into <see cref="PackedMemorySnapshot.managedTypes"/>.
        /// <para/>
        /// ???: Not sure about this - this is either a reference to the base type or <see cref="managedTypesArrayIndex"/>?
        /// But it is `None` when it's -1? WTF is going on here, Unity! 
        /// </summary>
        public readonly Option<PInt> baseOrElementTypeIndex;

        public Option<PInt> baseTypeIndex => 
            baseOrElementTypeIndex.valueOut(out var idx)
            ? idx == managedTypesArrayIndex ? None._ : Some(idx)
            : None._;
        
        /// <summary>
        /// Size in bytes of an instance of this type. If this type is an array type, this describes the amount of
        /// bytes a single element in the array will take up.
        /// </summary>
        /// <note>
        /// This is an <see cref="Either{A,B}"/> because sometimes Unity returns a negative number for size, which
        /// obviously makes no sense. We have `Left` here with the raw value on failure and `Right` value on success
        /// and then we try to fall-back gracefully as much as we can when this happens.
        /// </note>
        public readonly Either<int, PInt> size;

        /// <summary>
        /// The address in memory that contains the description of this type inside the virtual machine.
        /// <para/>
        /// This can be used to match managed objects in the heap to their corresponding TypeDescription, as the first
        /// pointer of a managed object points to its type description.
        /// </summary>
        public readonly ulong typeInfoAddress;

        /// <summary>
        /// This index is an index into the <see cref="PackedMemorySnapshot.managedTypes"/> array.
        /// </summary>
        public readonly PInt managedTypesArrayIndex;

        /// <summary>
        /// Index into <see cref="PackedMemorySnapshot.nativeTypes"/> if this managed type has a native counterpart or
        /// `None` otherwise.
        /// </summary>
        [NonSerialized]
        public Option<PInt> nativeTypeArrayIndex;

        /// <summary>
        /// Number of all objects of this type.
        /// </summary>
        [NonSerialized]
        public PInt totalObjectCount;

        /// <summary>
        /// The size of all objects of this type.
        /// </summary>
        [NonSerialized]
        public ulong totalObjectSize;

        /// <summary>
        /// Whether the type derived from <see cref="UnityEngine.Object"/>.
        /// </summary>
        [NonSerialized]
        public bool isUnityEngineObject;

        /// <summary>
        /// Whether the type contains any field of ReferenceType
        /// </summary>
        [NonSerialized]
        public bool containsFieldOfReferenceType;

        /// <summary>
        /// Whether this or a base class contains any field of a ReferenceType.
        /// </summary>
        [NonSerialized]
        public bool containsFieldOfReferenceTypeInInheritanceChain;

        public PackedManagedType(
            bool isValueType, Option<PInt> arrayRank, string name, string assembly, PackedManagedField[] fields, 
            byte[] staticFieldBytes, Option<PInt> baseOrElementTypeIndex, Either<int, PInt> size, ulong typeInfoAddress, 
            PInt managedTypesArrayIndex
        ) {
            this.isValueType = isValueType;
            this.arrayRank = arrayRank;
            this.name = name;
            this.assembly = assembly;
            this.fields = fields;
            this.staticFieldBytes = staticFieldBytes;
            this.baseOrElementTypeIndex = baseOrElementTypeIndex;
            this.size = size;
            this.typeInfoAddress = typeInfoAddress;
            this.managedTypesArrayIndex = managedTypesArrayIndex;
        }

        /// <inheritdoc/>
        string PackedMemorySnapshot.TypeForSubclassSearch.name => name;

        /// <inheritdoc/>
        PInt PackedMemorySnapshot.TypeForSubclassSearch.typeArrayIndex => managedTypesArrayIndex;

        /// <inheritdoc/>
        Option<PInt> PackedMemorySnapshot.TypeForSubclassSearch.baseTypeArrayIndex => baseOrElementTypeIndex;
        
        /// <summary>
        /// An array containing descriptions of all instance fields of this type.
        /// </summary>
        public PackedManagedField[] instanceFields
        {
            get
            {
                if (m_InstanceFields == null)
                {
                    // Find how many instance fields there are
                    var count = 0;
                    for (var n = 0; n < fields.Length; ++n)
                    {
                        if (!fields[n].isStatic)
                            count++;
                    }

                    // Allocate an array to hold just the instance fields
                    m_InstanceFields = new PackedManagedField[count];
                    count = 0;

                    // Copy instance field descriptions
                    for (var n = 0; n < fields.Length; ++n)
                    {
                        if (!fields[n].isStatic)
                        {
                            m_InstanceFields[count] = fields[n];
                            count++;
                        }
                    }
                }

                return m_InstanceFields;
            }
        }
        [NonSerialized] PackedManagedField[] m_InstanceFields;

        /// <summary>
        /// An array containing descriptions of all static fields of this type, NOT including static fields of base
        /// type.
        /// </summary>
        public PackedManagedField[] staticFields
        {
            get
            {
                if (m_StaticFields == null)
                {
                    if (staticFieldBytes == null || staticFieldBytes.Length == 0)
                    {
                        m_StaticFields = new PackedManagedField[0];
                    }
                    else
                    {
                        // Find how many static fields there are
                        var count = 0;
                        for (var n = 0; n < fields.Length; ++n)
                        {
                            if (fields[n].isStatic)
                                count++;
                        }

                        // Allocate an array to hold just the static fields
                        m_StaticFields = new PackedManagedField[count];
                        count = 0;

                        // Copy static field descriptions
                        for (var n = 0; n < fields.Length; ++n)
                        {
                            if (fields[n].isStatic)
                            {
                                m_StaticFields[count] = fields[n];
                                count++;
                            }
                        }
                    }
                }

                return m_StaticFields;
            }
        }
        [NonSerialized] PackedManagedField[] m_StaticFields;

        /// <summary>
        /// Gets whether this is a common language runtime primitive type.
        /// </summary>
        public bool isPrimitive
        {
            get
            {
                switch (name)
                {
                    case "System.Char":
                    case "System.Byte":
                    case "System.SByte":
                    case "System.Int16":
                    case "System.UInt16":
                    case "System.Int32":
                    case "System.UInt32":
                    case "System.Int64":
                    case "System.UInt64":
                    case "System.Single":
                    case "System.Double":
                    case "System.Decimal":
                    case "System.Boolean":
                    case "System.String":
                    case "System.Object":
                    case "System.IntPtr":
                    case "System.UIntPtr":
                    case "System.Enum":
                    case "System.ValueType":
                    case "System.ReferenceType":
                        return true;
                    default:
                        return false;
                }
            }
        }

        /// <summary>
        /// Gets whether the type is a pointer. This includes ReferenceTypes, IntPtr and UIntPtr.
        /// </summary>
        public bool isPointer
        {
            get
            {
                if (!isValueType)
                    return true;

                switch (name)
                {
                    case "System.IntPtr":
                    case "System.UIntPtr":
                        return true;
                    default:
                        return false;
                }
            }
        }

        public bool isDerivedReferenceType
        {
            get
            {
                if (isValueType)
                    return false;

                if (!this.baseOrElementTypeIndex.valueOut(out var baseOrElementTypeIndex))
                    return false;

                if (baseOrElementTypeIndex == managedTypesArrayIndex)
                    return false;

                return true;
            }
        }

        /// <summary>
        /// <para/>
        /// An enum derives from <see cref="System.Enum"/>, which derives from <see cref="System.ValueType"/>.
        /// </summary>
        public bool isDerivedValueType
        {
            get
            {
                if (isReferenceType)
                    return false;

                if (!this.baseOrElementTypeIndex.valueOut(out var baseOrElementTypeIndex))
                    return false;

                if (baseOrElementTypeIndex == managedTypesArrayIndex)
                    return false;

                return true;
            }
        }

        public bool TryGetField(string name, out PackedManagedField field)
        {
            for (int n=0, nend = fields.Length; n < nend; ++n)
            {
                if (fields[n].name == name)
                {
                    field = fields[n];
                    return true;
                }
            }

            field = default;
            return false;
        }

        const int k_Version = 1;

        public static void Write(System.IO.BinaryWriter writer, PackedManagedType[] value)
        {
            writer.Write(k_Version);
            writer.Write(value.Length);

            for (int n = 0, nend = value.Length; n < nend; ++n)
            {
                writer.Write(value[n].isValueType);
                writer.Write(value[n].arrayRank.isSome);
                writer.Write(value[n].arrayRank.fold(0, _ => _.asInt));
                writer.Write(value[n].name);
                writer.Write(value[n].assembly);

                writer.Write(value[n].staticFieldBytes.Length);
                writer.Write(value[n].staticFieldBytes);
                writer.Write(value[n].baseOrElementTypeIndex.fold(-1, _ => _));
                writer.Write(value[n].size.fold(v => v, v => v));
                writer.Write(value[n].typeInfoAddress);
                writer.Write(value[n].managedTypesArrayIndex);

                PackedManagedField.Write(writer, value[n].fields);
            }
        }

        public static void Read(System.IO.BinaryReader reader, out PackedManagedType[] value, out string stateString)
        {
            value = new PackedManagedType[0];
            stateString = "";

            var version = reader.ReadInt32();
            if (version >= 1)
            {
                var length = reader.ReadInt32();
                stateString = $"Loading {length} Managed Types";
                value = new PackedManagedType[length];

                for (int n = 0, nend = value.Length; n < nend; ++n)
                {
                    var isValueType = reader.ReadBoolean();
                    var isArray = reader.ReadBoolean();
                    var arrayRank = reader.ReadInt32();
                    
                    var name = reader.ReadString();
                    // Types without namespace have a preceding period, which we remove here
                    // https://issuetracker.unity3d.com/issues/packedmemorysnapshot-leading-period-symbol-in-typename
                    if (name != null && name.Length > 0 && name[0] == '.')
                        name = value[n].name.Substring(1);
                    
                    var assembly = reader.ReadString();

                    var count = reader.ReadInt32();
                    var staticFieldBytes = reader.ReadBytes(count);
                    var rawBaseOrElementTypeIndex = reader.ReadInt32();
                    var baseOrElementTypeIndex = 
                        rawBaseOrElementTypeIndex == -1 ? None._ : Some(PInt.createOrThrow(rawBaseOrElementTypeIndex));
                    var size = reader.ReadInt32();
                    var typeInfoAddress = reader.ReadUInt64();
                    var managedTypesArrayIndex = reader.ReadInt32();

                    PackedManagedField.Read(reader, out var fields);

                    value[n] = new PackedManagedType(
                        isValueType: isValueType,
                        arrayRank: isArray ? Some(PInt.createOrThrow(arrayRank)) : None._,
                        name: name,
                        assembly: assembly,
                        staticFieldBytes: staticFieldBytes,
                        baseOrElementTypeIndex: baseOrElementTypeIndex,
                        size: PInt.createEither(size),
                        typeInfoAddress: typeInfoAddress,
                        managedTypesArrayIndex: PInt.createOrThrow(managedTypesArrayIndex),
                        fields: fields
                    );
                }
            }
        }

        public static PackedManagedType[] FromMemoryProfiler(
            UnityEditor.Profiling.Memory.Experimental.PackedMemorySnapshot snapshot
        ) {
            var source = snapshot.typeDescriptions;
            var managedTypes = new PackedManagedType[source.GetNumEntries()];

            var sourceAssembly = new string[source.assembly.GetNumEntries()];
            source.assembly.GetEntries(0, source.assembly.GetNumEntries(), ref sourceAssembly);

            var sourceFlags = new TypeFlags[source.flags.GetNumEntries()];
            source.flags.GetEntries(0, source.flags.GetNumEntries(), ref sourceFlags);

            var sourceName = new string[source.typeDescriptionName.GetNumEntries()];
            source.typeDescriptionName.GetEntries(0, source.typeDescriptionName.GetNumEntries(), ref sourceName);

            var sourceSize = new int[source.size.GetNumEntries()];
            source.size.GetEntries(0, source.size.GetNumEntries(), ref sourceSize);

            var sourceTypeInfoAddress = new ulong[source.typeInfoAddress.GetNumEntries()];
            source.typeInfoAddress.GetEntries(0, source.typeInfoAddress.GetNumEntries(), ref sourceTypeInfoAddress);

            var sourceTypeIndex = new int[source.typeIndex.GetNumEntries()];
            source.typeIndex.GetEntries(0, source.typeIndex.GetNumEntries(), ref sourceTypeIndex);

            var sourceBaseOrElementTypeIndex = new int[source.baseOrElementTypeIndex.GetNumEntries()];
            source.baseOrElementTypeIndex.GetEntries(0, source.baseOrElementTypeIndex.GetNumEntries(), ref sourceBaseOrElementTypeIndex);

            var sourceStaticFieldBytes = new byte[source.staticFieldBytes.GetNumEntries()][];
            source.staticFieldBytes.GetEntries(0, source.staticFieldBytes.GetNumEntries(), ref sourceStaticFieldBytes);

            var sourceFieldIndices = new int[source.fieldIndices.GetNumEntries()][];
            source.fieldIndices.GetEntries(0, source.fieldIndices.GetNumEntries(), ref sourceFieldIndices);

            // fields
            var desc = snapshot.fieldDescriptions;

            var fieldName = new string[desc.fieldDescriptionName.GetNumEntries()];
            desc.fieldDescriptionName.GetEntries(0, desc.fieldDescriptionName.GetNumEntries(), ref fieldName);

            var fieldStatic = new bool[desc.isStatic.GetNumEntries()];
            desc.isStatic.GetEntries(0, desc.isStatic.GetNumEntries(), ref fieldStatic);

            var fieldOffset = new int[desc.offset.GetNumEntries()];
            desc.offset.GetEntries(0, desc.offset.GetNumEntries(), ref fieldOffset);

            var fieldTypeIndex = new int[desc.typeIndex.GetNumEntries()];
            desc.typeIndex.GetEntries(0, desc.typeIndex.GetNumEntries(), ref fieldTypeIndex);

            var sourceFieldDescriptions = new Option<PackedManagedField>[desc.GetNumEntries()];
            for (int n=0, nend=sourceFieldDescriptions.Length; n < nend; ++n) {
                var name = fieldName[n];
                var isStatic = fieldStatic[n];
                var rawOffset = fieldOffset[n];
                var maybeOffset = PInt.create(rawOffset);
                var rawManagedTypesArrayIndex = fieldTypeIndex[n];
                var maybeManagedTypesArrayIndex = PInt.create(rawManagedTypesArrayIndex);

                if (
                    maybeOffset.valueOut(out var offset) 
                    && maybeManagedTypesArrayIndex.valueOut(out var managedTypesArrayIndex)
                ) {
                    sourceFieldDescriptions[n] = Some(new PackedManagedField(
                        name: name, isStatic: isStatic, offset: offset, managedTypesArrayIndex: managedTypesArrayIndex
                    ));
                }
            }

            // A cache for the temporary fields as we don't know how many of them are valid.
            var fieldsList = new List<PackedManagedField>();
            for (int n = 0, nend = managedTypes.Length; n < nend; ++n) {
                var rawBaseOrElementTypeIndex = sourceBaseOrElementTypeIndex[n];
                var sourceFieldIndicesForValue = sourceFieldIndices[n];

                // Assign fields.
                fieldsList.Clear();
                for (var j=0; j < sourceFieldIndicesForValue.Length; ++j) {
                    var i = sourceFieldIndicesForValue[j];
                    var maybeField = sourceFieldDescriptions[i];
                    
                    // Skip invalid fields.
                    if (maybeField.valueOut(out var field)) {
                        fieldsList.Add(new PackedManagedField(
                            name: field.name,
                            offset: field.offset,
                            isStatic: field.isStatic,
                            managedTypesArrayIndex: field.managedTypesArrayIndex
                        ));
                    }
                }
                var fields = fieldsList.ToArray();

                var name = sourceName[n];
                // namespace-less types have a preceding dot, which we remove here
                if (name != null && name.Length > 0 && name[0] == '.') name = name.Substring(1);

                var isValueType = (sourceFlags[n] & TypeFlags.kValueType) != 0;
                var isArray =
                    (sourceFlags[n] & TypeFlags.kArray) != 0
                        ? Some(PInt.createOrThrow((int) (sourceFlags[n] & TypeFlags.kArrayRankMask) >> 16))
                        : None._;
                var baseOrElementTypeIndex =
                    rawBaseOrElementTypeIndex == -1 ? None._ : Some(PInt.createOrThrow(rawBaseOrElementTypeIndex));
                var rawManagedTypesArrayIndex = sourceTypeIndex[n];
                var size = PInt.createEither(sourceSize[n]);
                var managedTypesArrayIndex = PInt.createOrThrow(rawManagedTypesArrayIndex);
                
                managedTypes[n] = new PackedManagedType(
                    isValueType: isValueType,
                    arrayRank: isArray,
                    name: name,
                    assembly: sourceAssembly[n],
                    staticFieldBytes: sourceStaticFieldBytes[n],
                    baseOrElementTypeIndex: baseOrElementTypeIndex,
                    size: size,
                    typeInfoAddress: sourceTypeInfoAddress[n],
                    managedTypesArrayIndex: managedTypesArrayIndex,
                    fields: fields
                );
            }

            var importFailureIndexes = 
                sourceFieldDescriptions
                .Select((opt, idx) => (opt, idx))
                .Where(tpl => tpl.opt.isNone)
                .Select(tpl => tpl.idx)
                .ToArray();
            if (importFailureIndexes.Length > 0) {
                bool isThreadStatic(int idx) => PackedManagedField.isThreadStatic(fieldStatic[idx], fieldOffset[idx]);
                
                var threadStatics = importFailureIndexes.Where(isThreadStatic).ToArray();
                reportFailures(
                    "Detected following fields as [ThreadStatic] static fields. We do not know how to determine the "
                    + $"memory location of these fields, thus we can not crawl them. Take that in mind",
                    threadStatics
                );
                var others = importFailureIndexes.Where(idx => !isThreadStatic(idx)).ToArray();
                reportFailures(
                    "Failed to import fields from the Unity memory snapshot due to invalid values, this seems "
                    + "like a Unity bug", 
                    others
                );

                void reportFailures(string description, int[] failureIndexes) {
                    if (failureIndexes.Length == 0) return;
                
                    // Group failures to not overwhelm Unity console window.
                    var groupedFailures = failureIndexes.OrderBy(_ => _).groupedIn(PInt.createOrThrow(100)).ToArray();

                    for (int idx = 0, idxEnd = groupedFailures.Length; idx < idxEnd; idx++) {
                        var group = groupedFailures[idx];
                        var str = string.Join("\n\n", group.Select(_idx => {
                            var managedTypesArrayIndex = fieldTypeIndex[_idx];
                            var typeName = managedTypes[managedTypesArrayIndex].name;
                            var typeAssembly = managedTypes[managedTypesArrayIndex].assembly;
                            return $"Field[index={_idx}, name={fieldName[_idx]}, static={fieldStatic[_idx]}, "
                                   + $"offset={fieldOffset[_idx]}, managedTypesArrayIndex={managedTypesArrayIndex}"
                                   + "]\n"
                                   + $"@ [assembly '{typeAssembly}'] [type '{typeName}']";
                        }));
                    
                        Debug.LogWarning($"HeapExplorer: {description}:\n{str}");
                    }
                }
            }
            
            return managedTypes;
        }

        public override string ToString()
        {
            var text = $"name: {name}, isValueType: {isValueType}, isArray: {arrayRank}, size: {size}";
            return text;
        }
    }

    public static class PackedManagedTypeUtility
    {
        public static string GetInheritanceAsString(PackedMemorySnapshot snapshot, PInt managedTypesArrayIndex)
        {
            var sb = new System.Text.StringBuilder(128);
            GetInheritanceAsString(snapshot, managedTypesArrayIndex, sb);
            return sb.ToString();
        }

        public static void GetInheritanceAsString(
            PackedMemorySnapshot snapshot, PInt managedTypesArrayIndex, System.Text.StringBuilder target
        ) {
            var depth = 0;
            var cycleTracker = new CycleTracker<int>();

            var maybeCurrentManagedTypesArrayIndex = Some(managedTypesArrayIndex);
            cycleTracker.markStartOfSearch();
            {while (maybeCurrentManagedTypesArrayIndex.valueOut(out var currentManagedTypesArrayIndex)) {
                if (cycleTracker.markIteration(currentManagedTypesArrayIndex)) {
                    cycleTracker.reportCycle(
                        $"{nameof(GetInheritanceAsString)}()", currentManagedTypesArrayIndex,
                        idx => snapshot.managedTypes[idx].ToString()
                    );
                    break;
                }
                
                for (var n = 0; n < depth; ++n)
                    target.Append("  ");

                target.AppendFormat("{0}\n", snapshot.managedTypes[currentManagedTypesArrayIndex].name);
                depth++;

                maybeCurrentManagedTypesArrayIndex = 
                    snapshot.managedTypes[currentManagedTypesArrayIndex].baseOrElementTypeIndex;
            }}
        }

        /// <summary>
        /// Gets whether any type in its inheritance chain has an instance field.
        /// </summary>
        public static bool HasTypeOrBaseAnyField(
            PackedMemorySnapshot snapshot, PackedManagedType type, bool checkInstance, bool checkStatic
        ) {
            var cycleTracker = new CycleTracker<int>();
            do
            {
                if (cycleTracker.markIteration(type.managedTypesArrayIndex)) {
                    cycleTracker.reportCycle(
                        $"{nameof(HasTypeOrBaseAnyField)}()", type.managedTypesArrayIndex,
                        idx => snapshot.managedTypes[idx].ToString()
                    );
                    break;
                }

                if (checkInstance && type.instanceFields.Length > 0)
                    return true;

                if (checkStatic && type.staticFields.Length > 0)
                    return true;

                {if (type.baseOrElementTypeIndex.valueOut(out var baseOrElementTypeIndex))
                    type = snapshot.managedTypes[baseOrElementTypeIndex];}

            } while (type.baseOrElementTypeIndex.isSome && Some(type.managedTypesArrayIndex) != type.baseOrElementTypeIndex);

            return false;
        }

        /// <summary>
        /// Gets whether any type in its inheritance chain has an instance field.
        /// </summary>
        public static bool HasTypeOrBaseAnyInstanceField(PackedMemorySnapshot snapshot, PackedManagedType type)
        {
            var loopGuard = 0;
            do
            {
                if (++loopGuard > 64)
                {
                    Debug.LogError("loopguard kicked in");
                    break;
                }

                if (type.instanceFields.Length > 0)
                    return true;

                {if (type.baseOrElementTypeIndex.valueOut(out var baseOrElementTypeIndex))
                    type = snapshot.managedTypes[baseOrElementTypeIndex];}

            } while (type.baseOrElementTypeIndex.isSome && Some(type.managedTypesArrayIndex) != type.baseOrElementTypeIndex);

            return false;
        }

        public static bool HasTypeOrBaseAnyInstanceField(PackedMemorySnapshot snapshot, PackedManagedType type, out PackedManagedType fieldType)
        {
            var loopGuard = 0;
            do
            {
                if (++loopGuard > 64)
                {
                    Debug.LogError("HeapExplorer: loopguard kicked in");
                    break;
                }

                if (type.instanceFields.Length > 0)
                {
                    fieldType = snapshot.managedTypes[type.instanceFields[0].managedTypesArrayIndex];
                    return true;
                }

                if (type.baseOrElementTypeIndex.valueOut(out var baseOrElementTypeIndex))
                    type = snapshot.managedTypes[baseOrElementTypeIndex];

            } while (type.baseOrElementTypeIndex.isSome && Some(type.managedTypesArrayIndex) != type.baseOrElementTypeIndex);

            fieldType = default;
            return false;
        }

        public static bool HasTypeOrBaseAnyStaticField(PackedMemorySnapshot snapshot, PackedManagedType type, out PackedManagedType fieldType)
        {
            var loopGuard = 0;
            do
            {
                if (++loopGuard > 64)
                {
                    Debug.LogError("HeapExplorer: loopguard kicked in");
                    break;
                }

                if (type.staticFields.Length > 0)
                {
                    fieldType = snapshot.managedTypes[type.staticFields[0].managedTypesArrayIndex];
                    return true;
                }

                {if (type.baseOrElementTypeIndex.valueOut(out var baseOrElementTypeIndex))
                    type = snapshot.managedTypes[baseOrElementTypeIndex];}

            } while (type.baseOrElementTypeIndex.isSome && Some(type.managedTypesArrayIndex) != type.baseOrElementTypeIndex);

            fieldType = default;
            return false;
        }
    }
}
