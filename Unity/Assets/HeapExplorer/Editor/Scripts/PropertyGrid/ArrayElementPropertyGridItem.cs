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
            displayValue = m_MemoryReader.ReadFieldValueAsString(address, type);
            isExpandable = address > 0;
            icon = HeEditorStyles.GetTypeImage(m_Snapshot, type);

            if (type.isPointer)
            {
                var pointer = m_MemoryReader.ReadPointer(address);
                isExpandable = pointer > 0;
                enabled = pointer > 0;
            }
        }

        protected override void OnBuildChildren(System.Action<BuildChildrenArgs> add)
        {
            // this class is only a container for whatever ArrayPropertyGridItem is tossed in
        }
    }
}
