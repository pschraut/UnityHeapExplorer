//
// Heap Explorer for Unity. Copyright (c) 2019 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://bitbucket.org/pschraut/unityheapexplorer/
//
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;

namespace HeapExplorer
{
    public class ReferenceTypePropertyGridItem : PropertyGridItem
    {
        public PackedManagedField field;
        ulong m_Pointer;

        public ReferenceTypePropertyGridItem(PropertyGridControl owner, PackedMemorySnapshot snapshot, System.UInt64 address, AbstractMemoryReader memoryReader)
            : base(owner, snapshot, address, memoryReader)
        {
        }

        protected override void OnInitialize()
        {
            m_Pointer = m_MemoryReader.ReadPointer(address);

            // If it's a pointer, read the actual object type from the object in memory
            // and do not rely on the field type. The reason for this is that the field type
            // could be just the base-type or an interface, but the want to display the actual object type instead-
            var type = m_Snapshot.managedTypes[field.managedTypesArrayIndex];
            if (type.isPointer && m_Pointer != 0)
            {
                var i = m_Snapshot.FindManagedObjectTypeOfAddress(m_Pointer);
                if (i != -1)
                    type = m_Snapshot.managedTypes[i];
            }

            base.type = type;
            displayName = field.name;
            displayType = type.name;
            displayValue = m_MemoryReader.ReadFieldValueAsString(address, type);
            isExpandable = false;// m_pointer > 0 && PackedManageTypeUtility.HasTypeOrBaseAnyInstanceField(m_snapshot, type);
            enabled = m_Pointer > 0;
            icon = HeEditorStyles.GetTypeImage(m_Snapshot, type);

            if (field.isStatic)
            {
                displayType = "static " + displayType;

                //PackedManagedType firstType;
                //if (PackedManageTypeUtility.HasTypeOrBaseAnyStaticField(m_snapshot, type, out firstType))
                //    allowExpand = m_pointer > 0 && firstType.managedTypeArrayIndex != type.managedTypeArrayIndex;
            }
            //else
            //{
            //    // HashString has a HashString static field (HashString.Empty)
            //    // The following line makes sure that we cannot infinitely expand the same type
            //    PackedManagedType firstType;
            //    if (PackedManageTypeUtility.HasTypeOrBaseAnyInstanceField(m_snapshot, type, out firstType))
            //        allowExpand = m_pointer > 0 && firstType.managedTypeArrayIndex != type.managedTypeArrayIndex;
            //}

            // HashString has a HashString static field (HashString.Empty)
            // The following line makes sure that we cannot infinitely expand the same type
            PackedManagedType firstType;
            if (PackedManagedTypeUtility.HasTypeOrBaseAnyInstanceField(m_Snapshot, type, out firstType))
                isExpandable = m_Pointer > 0 && firstType.managedTypesArrayIndex != type.managedTypesArrayIndex;
        }

        protected override void OnBuildChildren(System.Action<BuildChildrenArgs> add)
        {
            var args = new BuildChildrenArgs
            {
                parent = this,
                type = type,
                address = m_Pointer,
                memoryReader = new MemoryReader(m_Snapshot),
            };
            add(args);
        }
    }
}
