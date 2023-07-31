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
    public class PrimitiveTypePropertyGridItem : PropertyGridItem
    {
        public PackedManagedField field;

        public PrimitiveTypePropertyGridItem(PropertyGridControl owner, PackedMemorySnapshot snapshot, System.UInt64 address, AbstractMemoryReader memoryReader)
            : base(owner, snapshot, address, memoryReader)
        {
        }

        protected override void OnInitialize()
        {
            var type = m_Snapshot.managedTypes[field.managedTypesArrayIndex];
            base.type = type;
            //typeIndex = type.managedTypesArrayIndex;
            displayType = type.name;
            if (field.isStatic)
                displayType = "static " + displayType;

            displayName = field.name;
            displayValue = m_MemoryReader.ReadFieldValueAsString(address, type).getOrElse("<reading error>");
            isExpandable = false;
            icon = HeEditorStyles.GetTypeImage(m_Snapshot, type);

            if (type.isPointer)
                enabled = m_MemoryReader.ReadPointer(address).contains(_ => _ > 0);
        }

        protected override void OnBuildChildren(System.Action<BuildChildrenArgs> add)
        {
            var args = new BuildChildrenArgs();
            args.parent = this;
            args.type = m_Snapshot.managedTypes[field.managedTypesArrayIndex];
            args.address = address;
            args.memoryReader = field.isStatic ? (AbstractMemoryReader)(new StaticMemoryReader(m_Snapshot, args.type.staticFieldBytes)) : (AbstractMemoryReader)(new MemoryReader(m_Snapshot));// m_memoryReader;
            add(args);
        }
    }
}
