using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;

namespace HeapExplorer
{
    public class BaseClassPropertyGridItem : PropertyGridItem
    {
        public int baseClassTypeIndex;

        public BaseClassPropertyGridItem(PropertyGridControl owner, PackedMemorySnapshot snapshot, System.UInt64 address, AbstractMemoryReader memoryReader)
            : base(owner, snapshot, address, memoryReader)
        {
            baseClassTypeIndex = -1;
        }

        protected override void OnInitialize()
        {
            var baseType = m_snapshot.managedTypes[baseClassTypeIndex];
            m_type = baseType;
            typeIndex = baseType.managedTypesArrayIndex;
            displayType = baseType.name;
            displayName = "base";
            displayValue = baseType.name;
            allowExpand = true;
            //if (baseType.baseOrElementTypeIndex == baseClassTypeIndex)
            //    allowExpand = false;
            //if (baseType.baseOrElementTypeIndex == m_snapshot.coreTypes.systemObject.managedTypeArrayIndex)
            //    allowExpand = false;
            //if (!PackedManageTypeUtility.HasTypeOrBaseAnyField(m_snapshot, baseType))
            //    allowExpand = false;
        }

        protected override void OnBuildChildren(System.Action<BuildChildrenArgs> add)
        {
            var args = new BuildChildrenArgs
            {
                parent = this,
                type = m_snapshot.managedTypes[baseClassTypeIndex],
                address = address,
                memoryReader = m_memoryReader,
                //addInstance = true
            };
            add(args);
        }
    }
}
