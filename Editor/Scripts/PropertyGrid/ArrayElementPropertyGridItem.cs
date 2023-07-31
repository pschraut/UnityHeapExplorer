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
    public class ArrayElementPropertyGridItem : PropertyGridItem
    {
        public ArrayElementPropertyGridItem(PropertyGridControl owner, PackedMemorySnapshot snapshot, System.UInt64 address, AbstractMemoryReader memoryReader)
            : base(owner, snapshot, address, memoryReader)
        {
        }

        protected override void OnInitialize()
        {
            displayType = type.name;
            displayName = "element";
            displayValue = m_MemoryReader.ReadFieldValueAsString(address, type).getOrElse("<cannot read>");
            isExpandable = address > 0;
            icon = HeEditorStyles.GetTypeImage(m_Snapshot, type);

            if (type.isPointer)
            {
                var pointer = m_MemoryReader.ReadPointer(address);
                isExpandable = pointer.contains(_ => _ > 0);
                enabled = pointer.contains(_ => _ > 0);
            }
        }

        protected override void OnBuildChildren(System.Action<BuildChildrenArgs> add)
        {
            // this class is only a container for whatever ArrayPropertyGridItem is tossed in
        }
    }
}
