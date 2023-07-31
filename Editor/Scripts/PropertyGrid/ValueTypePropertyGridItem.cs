//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//
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
            type = m_Snapshot.managedTypes[field.managedTypesArrayIndex];
            displayName = field.name;
            displayType = type.name;
            displayValue = m_MemoryReader.ReadFieldValueAsString(address, type).getOrElse("<cannot read>");
            isExpandable = false;

            if (field.isStatic)
            {
                displayType = "static " + displayType;

                //PackedManagedType firstType;
                //if (PackedManageTypeUtility.HasTypeOrBaseAnyStaticField(m_snapshot, m_Type, out firstType))
                //    allowExpand = firstType.managedTypeArrayIndex != m_Type.managedTypeArrayIndex;
            }
            //else
            //{
            //    // HashString has a HashString static field (HashString.Empty)
            //    // The following line makes sure that we cannot infinitely expand the same type
            //    PackedManagedType firstType;
            //    if (PackedManageTypeUtility.HasTypeOrBaseAnyInstanceField(m_snapshot, m_Type, out firstType))
            //        allowExpand = firstType.managedTypeArrayIndex != m_Type.managedTypeArrayIndex;
            //}

            // HashString has a HashString static field (HashString.Empty)
            // The following line makes sure that we cannot infinitely expand the same type
            PackedManagedType firstType;
            if (PackedManagedTypeUtility.HasTypeOrBaseAnyInstanceField(m_Snapshot, type, out firstType))
                isExpandable = firstType.managedTypesArrayIndex != type.managedTypesArrayIndex;

            icon = HeEditorStyles.GetTypeImage(m_Snapshot, type);
        }

        protected override void OnBuildChildren(System.Action<BuildChildrenArgs> add)
        {
            var args = new BuildChildrenArgs {
                parent = this,
                type = m_Snapshot.managedTypes[field.managedTypesArrayIndex],
                address = address - m_Snapshot.virtualMachineInformation.objectHeaderSize
            };
            args.memoryReader = field.isStatic ? (AbstractMemoryReader)(new StaticMemoryReader(m_Snapshot, args.type.staticFieldBytes)) : (AbstractMemoryReader)(new MemoryReader(m_Snapshot));// m_memoryReader;
            add(args);
        }
    }
}
