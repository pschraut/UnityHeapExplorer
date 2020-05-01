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
    public class BaseClassPropertyGridItem : PropertyGridItem
    {
        public BaseClassPropertyGridItem(PropertyGridControl owner, PackedMemorySnapshot snapshot, System.UInt64 address, AbstractMemoryReader memoryReader)
            : base(owner, snapshot, address, memoryReader)
        {
        }

        protected override void OnInitialize()
        {
            displayType = type.name;
            displayName = "base";
            displayValue = type.name;
            isExpandable = true;
        }

        protected override void OnBuildChildren(System.Action<BuildChildrenArgs> add)
        {
            var args = new BuildChildrenArgs
            {
                parent = this,
                type = m_Snapshot.managedTypes[type.managedTypesArrayIndex],
                address = address,
                memoryReader = m_MemoryReader
            };
            add(args);
        }
    }
}
