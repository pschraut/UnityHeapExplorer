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
            var type = m_snapshot.managedTypes[field.managedTypesArrayIndex];
            m_type = type;
            typeIndex = type.managedTypesArrayIndex;
            displayType = type.name;
            if (field.isStatic)
                displayType = "static " + displayType;

            displayName = field.name;
            displayValue = m_memoryReader.ReadFieldValueAsString(address, type);
            allowExpand = false;
            icon = HeEditorStyles.GetTypeImage(m_snapshot, type);

            if (type.isPointer)
                enabled = m_memoryReader.ReadPointer(address) > 0;
        }

        protected override void OnBuildChildren(System.Action<BuildChildrenArgs> add)
        {
            var args = new BuildChildrenArgs();
            args.parent = this;
            args.type = m_snapshot.managedTypes[field.managedTypesArrayIndex];
            args.address = address;
            args.memoryReader = field.isStatic ? (AbstractMemoryReader)(new StaticMemoryReader(m_snapshot, args.type.staticFieldBytes)) : (AbstractMemoryReader)(new MemoryReader(m_snapshot));// m_memoryReader;
            //args.addInstance = true;
            add(args);
        }
    }
}
