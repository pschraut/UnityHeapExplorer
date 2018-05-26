using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;

namespace HeapExplorer
{
    public class ArrayElementPropertyGridItem : PropertyGridItem
    {
        public PackedManagedType type;

        public ArrayElementPropertyGridItem(PropertyGridControl owner, PackedMemorySnapshot snapshot, System.UInt64 address, AbstractMemoryReader memoryReader)
            : base(owner, snapshot, address, memoryReader)
        {
        }

        protected override void OnInitialize()
        {
            typeIndex = type.managedTypesArrayIndex;
            m_type = type;
            displayType = type.name;
            displayName = "element";
            displayValue = m_memoryReader.ReadFieldValueAsString(address, type);
            allowExpand = address > 0;
            icon = HeEditorStyles.GetTypeImage(m_snapshot, type);

            if (type.isPointer)
            {
                var pointer = m_memoryReader.ReadPointer(address);
                allowExpand = pointer > 0;
                enabled = pointer > 0;
            }
        }

        protected override void OnBuildChildren(System.Action<BuildChildrenArgs> add)
        {
            // this class is only a container for whatever ArrayPropertyGridItem is tossing in
        }
    }
}
