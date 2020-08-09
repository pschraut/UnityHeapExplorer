//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using UnityEditor.Profiling.Memory.Experimental;

namespace HeapExplorer
{
    // Description of a managed type.
    [Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    public struct PackedManagedType
    {
        public static readonly PackedManagedType invalid = new PackedManagedType
        {
            managedTypesArrayIndex = -1,
            nativeTypeArrayIndex = -1,
            baseOrElementTypeIndex = -1
        };

        // Is this type a value type? (if it's not a value type, it's a reference type)
        public System.Boolean isValueType;

        // Is this type an array?
        public System.Boolean isArray;

        // If this is an arrayType, this will return the rank of the array. (1 for a 1-dimensional array, 2 for a 2-dimensional array, etc)
        public System.Int32 arrayRank;

        // Name of this type.
        public System.String name;

        // Name of the assembly this type was loaded from.
        public System.String assembly;

        // An array containing descriptions of all fields of this type.
        public PackedManagedField[] fields;

        // The actual contents of the bytes that store this types static fields, at the point of time when the snapshot was taken.
        public System.Byte[] staticFieldBytes;

        // The base type for this type, pointed to by an index into PackedMemorySnapshot.typeDescriptions.
        public System.Int32 baseOrElementTypeIndex;

        // Size in bytes of an instance of this type. If this type is an arraytype, this describes the amount of bytes a single element in the array will take up.
        public System.Int32 size;

        // The address in memory that contains the description of this type inside the virtual machine.
        // This can be used to match managed objects in the heap to their corresponding TypeDescription, as the first pointer of a managed object points to its type description.
        public System.UInt64 typeInfoAddress;

        // The typeIndex of this type. This index is an index into the PackedMemorySnapshot.typeDescriptions array.
        public System.Int32 managedTypesArrayIndex;

        // if this managed type has a native counterpart
        [NonSerialized]
        public System.Int32 nativeTypeArrayIndex;

        // Number of all objects of this type.
        [NonSerialized]
        public System.Int32 totalObjectCount;

        // The size of all objects of this type.
        [NonSerialized]
        public System.Int64 totalObjectSize;

        // gets whether the type derived from UnityEngine.Object
        [NonSerialized]
        public System.Boolean isUnityEngineObject;

        // gets whether the type contains any field of ReferenceType
        [NonSerialized]
        public System.Boolean containsFieldOfReferenceType;

        // gets whether this or a base class contains any field of a ReferenceType
        [NonSerialized]
        public System.Boolean containsFieldOfReferenceTypeInInheritenceChain;

        // An array containing descriptions of all instance fields of this type.
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

        // An array containing descriptions of all static fields of this type, NOT including static fields of base type.
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
                }

                return false;
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
                }

                return false;
            }
        }

        public bool isDerivedReferenceType
        {
            get
            {
                if (isValueType)
                    return false;

                if (baseOrElementTypeIndex == -1)
                    return false;

                if (baseOrElementTypeIndex == managedTypesArrayIndex)
                    return false;

                return true;
            }
        }

        // An enum derives from System.Enum, which derives from System.ValueType.
        public bool isDerivedValueType
        {
            get
            {
                if (!isValueType)
                    return false;

                if (baseOrElementTypeIndex == -1)
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

            field = new PackedManagedField();
            field.managedTypesArrayIndex = -1;
            return false;
        }

        const System.Int32 k_Version = 1;

        public static void Write(System.IO.BinaryWriter writer, PackedManagedType[] value)
        {
            writer.Write(k_Version);
            writer.Write(value.Length);

            for (int n = 0, nend = value.Length; n < nend; ++n)
            {
                writer.Write(value[n].isValueType);
                writer.Write(value[n].isArray);
                writer.Write(value[n].arrayRank);
                writer.Write(value[n].name);
                writer.Write(value[n].assembly);

                writer.Write((System.Int32)value[n].staticFieldBytes.Length);
                writer.Write(value[n].staticFieldBytes);
                writer.Write(value[n].baseOrElementTypeIndex);
                writer.Write(value[n].size);
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
                stateString = string.Format("Loading {0} Managed Types", length);
                value = new PackedManagedType[length];

                for (int n = 0, nend = value.Length; n < nend; ++n)
                {
                    value[n].isValueType = reader.ReadBoolean();
                    value[n].isArray = reader.ReadBoolean();
                    value[n].arrayRank = reader.ReadInt32();
                    value[n].name = reader.ReadString();
                    value[n].assembly = reader.ReadString();

                    var count = reader.ReadInt32();
                    value[n].staticFieldBytes = reader.ReadBytes(count);
                    value[n].baseOrElementTypeIndex = reader.ReadInt32();
                    value[n].size = reader.ReadInt32();
                    value[n].typeInfoAddress = reader.ReadUInt64();
                    value[n].managedTypesArrayIndex = reader.ReadInt32();

                    PackedManagedField.Read(reader, out value[n].fields);

                    // Types without namespace have a preceding period, which we remove here
                    // https://issuetracker.unity3d.com/issues/packedmemorysnapshot-leading-period-symbol-in-typename
                    if (value[n].name != null && value[n].name.Length > 0 && value[n].name[0] == '.')
                        value[n].name = value[n].name.Substring(1);

                    value[n].nativeTypeArrayIndex = -1;
                }
            }
        }

        public static PackedManagedType[] FromMemoryProfiler(UnityEditor.Profiling.Memory.Experimental.PackedMemorySnapshot snapshot)
        {
            var source = snapshot.typeDescriptions;
            var value = new PackedManagedType[source.GetNumEntries()];

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

            var sourceFieldDescriptions = new PackedManagedField[desc.GetNumEntries()];
            for (int n=0, nend = sourceFieldDescriptions.Length; n < nend; ++n)
            {
                sourceFieldDescriptions[n].name = fieldName[n];
                sourceFieldDescriptions[n].isStatic = fieldStatic[n];
                sourceFieldDescriptions[n].offset = fieldOffset[n];
                sourceFieldDescriptions[n].managedTypesArrayIndex = fieldTypeIndex[n];
            }

            for (int n = 0, nend = value.Length; n < nend; ++n)
            {
                value[n] = new PackedManagedType
                {
                    isValueType = (sourceFlags[n] & TypeFlags.kValueType) != 0,
                    isArray = (sourceFlags[n] & TypeFlags.kArray) != 0,
                    arrayRank = (int)(sourceFlags[n] & TypeFlags.kArrayRankMask)>>16,
                    name = sourceName[n],
                    assembly = sourceAssembly[n],
                    staticFieldBytes = sourceStaticFieldBytes[n],
                    baseOrElementTypeIndex = sourceBaseOrElementTypeIndex[n],
                    size = sourceSize[n],
                    typeInfoAddress = sourceTypeInfoAddress[n],
                    managedTypesArrayIndex = sourceTypeIndex[n],

                    nativeTypeArrayIndex = -1,
                };

                value[n].fields = new PackedManagedField[sourceFieldIndices[n].Length];
                for (var j=0; j< sourceFieldIndices[n].Length; ++j)
                {
                    var i = sourceFieldIndices[n][j];
                    value[n].fields[j].name = sourceFieldDescriptions[i].name;
                    value[n].fields[j].offset = sourceFieldDescriptions[i].offset;
                    value[n].fields[j].isStatic = sourceFieldDescriptions[i].isStatic;
                    value[n].fields[j].managedTypesArrayIndex = sourceFieldDescriptions[i].managedTypesArrayIndex;
                }

                // namespace-less types have a preceding dot, which we remove here
                if (value[n].name != null && value[n].name.Length > 0 && value[n].name[0] == '.')
                    value[n].name = value[n].name.Substring(1);

            }
            return value;
        }

        public override string ToString()
        {
            var text = string.Format("name: {0}, isValueType: {1}, isArray: {2}, size: {3}", name, isValueType, isArray, size);
            return text;
        }
    }

    public static class PackedManagedTypeUtility
    {
        public static string GetInheritanceAsString(PackedMemorySnapshot snapshot, int managedTypesArrayIndex)
        {
            var sb = new System.Text.StringBuilder(128);
            GetInheritanceAsString(snapshot, managedTypesArrayIndex, sb);
            return sb.ToString();
        }

        public static void GetInheritanceAsString(PackedMemorySnapshot snapshot, int managedTypesArrayIndex, System.Text.StringBuilder target)
        {
            var depth = 0;
            var loopguard = 0;

            while (managedTypesArrayIndex != -1)
            {
                for (var n = 0; n < depth; ++n)
                    target.Append("  ");

                target.AppendFormat("{0}\n", snapshot.managedTypes[managedTypesArrayIndex].name);
                depth++;

                managedTypesArrayIndex = snapshot.managedTypes[managedTypesArrayIndex].baseOrElementTypeIndex;
                if (++loopguard > 64)
                    break;
            }
        }

        /// <summary>
        /// Gets whether any type in its inheritance chain has an instance field.
        /// </summary>
        public static bool HasTypeOrBaseAnyField(PackedMemorySnapshot snapshot, PackedManagedType type, bool checkInstance, bool checkStatic)
        {
            var loopguard = 0;
            do
            {
                if (++loopguard > 64)
                {
                    Debug.LogError("loopguard kicked in");
                    break;
                }

                if (checkInstance && type.instanceFields.Length > 0)
                    return true;

                if (checkStatic && type.staticFields.Length > 0)
                    return true;

                if (type.baseOrElementTypeIndex != -1)
                    type = snapshot.managedTypes[type.baseOrElementTypeIndex];

            } while (type.baseOrElementTypeIndex != -1 && type.managedTypesArrayIndex != type.baseOrElementTypeIndex);

            return false;
        }

        /// <summary>
        /// Gets whether any type in its inheritance chain has an instance field.
        /// </summary>
        public static bool HasTypeOrBaseAnyInstanceField(PackedMemorySnapshot snapshot, PackedManagedType type)
        {
            var loopguard = 0;
            do
            {
                if (++loopguard > 64)
                {
                    Debug.LogError("loopguard kicked in");
                    break;
                }

                if (type.instanceFields.Length > 0)
                    return true;

                if (type.baseOrElementTypeIndex != -1)
                    type = snapshot.managedTypes[type.baseOrElementTypeIndex];

            } while (type.baseOrElementTypeIndex != -1 && type.managedTypesArrayIndex != type.baseOrElementTypeIndex);

            return false;
        }

        public static bool HasTypeOrBaseAnyInstanceField(PackedMemorySnapshot snapshot, PackedManagedType type, out PackedManagedType fieldType)
        {
            fieldType = new PackedManagedType();
            fieldType.managedTypesArrayIndex = -1;
            fieldType.nativeTypeArrayIndex = -1;
            fieldType.baseOrElementTypeIndex = -1;

            var loopguard = 0;
            do
            {
                if (++loopguard > 64)
                {
                    Debug.LogError("loopguard kicked in");
                    break;
                }

                if (type.instanceFields.Length > 0)
                {
                    fieldType = snapshot.managedTypes[type.instanceFields[0].managedTypesArrayIndex];
                    return true;
                }

                if (type.baseOrElementTypeIndex != -1)
                    type = snapshot.managedTypes[type.baseOrElementTypeIndex];

            } while (type.baseOrElementTypeIndex != -1 && type.managedTypesArrayIndex != type.baseOrElementTypeIndex);

            return false;
        }

        public static bool HasTypeOrBaseAnyStaticField(PackedMemorySnapshot snapshot, PackedManagedType type, out PackedManagedType fieldType)
        {
            fieldType = new PackedManagedType();
            fieldType.managedTypesArrayIndex = -1;
            fieldType.nativeTypeArrayIndex = -1;
            fieldType.baseOrElementTypeIndex = -1;

            var loopguard = 0;
            do
            {
                if (++loopguard > 64)
                {
                    Debug.LogError("loopguard kicked in");
                    break;
                }

                if (type.staticFields.Length > 0)
                {
                    fieldType = snapshot.managedTypes[type.staticFields[0].managedTypesArrayIndex];
                    return true;
                }

                if (type.baseOrElementTypeIndex != -1)
                    type = snapshot.managedTypes[type.baseOrElementTypeIndex];

            } while (type.baseOrElementTypeIndex != -1 && type.managedTypesArrayIndex != type.baseOrElementTypeIndex);

            return false;
        }
    }
}
