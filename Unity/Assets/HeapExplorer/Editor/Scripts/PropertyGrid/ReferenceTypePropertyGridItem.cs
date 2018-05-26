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
        ulong m_pointer;
        //MemoryReader m_heapMemoryReader;

        public ReferenceTypePropertyGridItem(PropertyGridControl owner, PackedMemorySnapshot snapshot, System.UInt64 address, AbstractMemoryReader memoryReader)
            : base(owner, snapshot, address, memoryReader)
        {
            //m_heapMemoryReader = new MemoryReader(snapshot);
        }

        protected override void OnInitialize()
        {
            m_pointer = m_memoryReader.ReadPointer(address);

            // If it's a pointer, read the actual object type from the object in memory
            // and do not rely on the field type. The reason for this is that the field type
            // could be just the base-type or an interface, but the want to display the actual object type instead-
            var type = m_snapshot.managedTypes[field.managedTypesArrayIndex];
            if (type.isPointer && m_pointer != 0)
            {
                var i = m_snapshot.FindManagedObjectTypeOfAddress(m_pointer);
                if (i != -1)
                    type = m_snapshot.managedTypes[i];
            }

            typeIndex = type.managedTypesArrayIndex;
            m_type = type;
            displayName = field.name;
            displayType = type.name;
            displayValue = m_memoryReader.ReadFieldValueAsString(address, type);
            allowExpand = false;// m_pointer > 0 && PackedManageTypeUtility.HasTypeOrBaseAnyInstanceField(m_snapshot, type);
            enabled = m_pointer > 0;
            icon = HeEditorStyles.GetTypeImage(m_snapshot, type);

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
            if (PackedManagedTypeUtility.HasTypeOrBaseAnyInstanceField(m_snapshot, type, out firstType))
                allowExpand = m_pointer > 0 && firstType.managedTypesArrayIndex != type.managedTypesArrayIndex;
        }

        protected override void OnBuildChildren(System.Action<BuildChildrenArgs> add)
        {
            var args = new BuildChildrenArgs
            {
                parent = this,
                type = m_type,// m_snapshot.managedTypes[typeIndex],
                address = m_pointer,
                memoryReader = new MemoryReader(m_snapshot),
                //memoryReader = field.isStatic ? (AbstractMemoryReader)(new StaticMemoryReader(m_snapshot, m_snapshot.managedTypes[typeIndex].staticFieldBytes)) : (AbstractMemoryReader)(new MemoryReader(m_snapshot))// m_memoryReader;
                //addInstance = true,
            };
            add(args);

            //add(this, m_snapshot.managedTypes[typeIndex], m_pointer);
        }
    }
}
