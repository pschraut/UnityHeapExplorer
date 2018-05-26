using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;

namespace HeapExplorer
{
    public class ValueTypePropertyGridItem : PropertyGridItem
    {
        public PackedManagedField field;

        public ValueTypePropertyGridItem(PropertyGridControl owner, PackedMemorySnapshot snapshot, System.UInt64 address, AbstractMemoryReader memoryReader)
            : base(owner, snapshot, address, memoryReader)
        {
        }

        protected override void OnInitialize()
        {
            var type = m_snapshot.managedTypes[field.managedTypesArrayIndex];
            m_type = type;
            typeIndex = type.managedTypesArrayIndex;
            displayName = field.name;
            displayType = type.name;
            displayValue = m_memoryReader.ReadFieldValueAsString(address, type);
            allowExpand = false;

            if (field.isStatic)
            {
                displayType = "static " + displayType;

                //PackedManagedType firstType;
                //if (PackedManageTypeUtility.HasTypeOrBaseAnyStaticField(m_snapshot, type, out firstType))
                //    allowExpand = firstType.managedTypeArrayIndex != type.managedTypeArrayIndex;
            }
            //else
            //{
            //    // HashString has a HashString static field (HashString.Empty)
            //    // The following line makes sure that we cannot infinitely expand the same type
            //    PackedManagedType firstType;
            //    if (PackedManageTypeUtility.HasTypeOrBaseAnyInstanceField(m_snapshot, type, out firstType))
            //        allowExpand = firstType.managedTypeArrayIndex != type.managedTypeArrayIndex;
            //}

            // HashString has a HashString static field (HashString.Empty)
            // The following line makes sure that we cannot infinitely expand the same type
            PackedManagedType firstType;
            if (PackedManagedTypeUtility.HasTypeOrBaseAnyInstanceField(m_snapshot, type, out firstType))
                allowExpand = firstType.managedTypesArrayIndex != type.managedTypesArrayIndex;

            icon = HeEditorStyles.GetTypeImage(m_snapshot, type);
        }

        protected override void OnBuildChildren(System.Action<BuildChildrenArgs> add)
        {
            var args = new BuildChildrenArgs();
            args.parent = this;
            args.type = m_snapshot.managedTypes[field.managedTypesArrayIndex];
            args.address = address - (ulong)m_snapshot.virtualMachineInformation.objectHeaderSize;
            //args.memoryReader = new MemoryReader(m_snapshot);
            args.memoryReader = field.isStatic ? (AbstractMemoryReader)(new StaticMemoryReader(m_snapshot, args.type.staticFieldBytes)) : (AbstractMemoryReader)(new MemoryReader(m_snapshot));// m_memoryReader;
            //args.addInstance = true;
            add(args);

            //add(this, m_snapshot.managedTypes[field.typeIndex], address - (ulong)m_snapshot.virtualMachineInformation.objectHeaderSize);
        }
    }
}
