using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;

namespace HeapExplorer
{
    enum PropertyGridColumn
    {
        Name,
        Value,
        Type
    }

    abstract public class PropertyGridItem : AbstractTreeViewItem
    {
        public System.UInt64 address;
        public int typeIndex;
        public bool allowExpand;
        public string displayType = "";
        public string displayValue = "";
        public string tooltip = "";

        public PackedManagedType m_type;

        public AbstractMemoryReader myMemoryReader
        {
            get
            {
                return m_memoryReader;
            }
        }

        protected AbstractMemoryReader m_memoryReader;
        protected PackedMemorySnapshot m_snapshot;
        protected PropertyGridControl m_owner;

        bool m_isInitialized;
        bool m_hasBuiltChildren;

        static int s_uniqueId = 1;

        public PropertyGridItem(PropertyGridControl owner, PackedMemorySnapshot snapshot, System.UInt64 address, AbstractMemoryReader memoryReader)
            : base()
        {
            m_owner = owner;

            if (children == null)
                children = new List<TreeViewItem>();

            base.id = s_uniqueId++;
            this.m_snapshot = snapshot;
            this.address = address;
            this.m_memoryReader = memoryReader;
            this.tooltip = this.GetType().Name;
        }

        public void Initialize()
        {
            if (m_isInitialized)
                return;
            m_isInitialized = true;

            OnInitialize();

            var text = string.Format("{0} {1} = {2}\n\n{3}", 
                displayType, 
                displayName,
                displayValue,
                PackedManagedTypeUtility.GetInheritanceAsString(m_snapshot, m_type.managedTypesArrayIndex));

            this.tooltip = text.Trim();
        }

        public class BuildChildrenArgs
        {
            public TreeViewItem parent;
            public PackedManagedType type;
            public System.UInt64 address;
            public AbstractMemoryReader memoryReader;
        }

        // TreeViewItem target, TypeDescription type, System.UInt64 address
        public void BuildChildren(System.Action<BuildChildrenArgs> add)
        {
            if (m_hasBuiltChildren)
                return;
            m_hasBuiltChildren = true;

            OnBuildChildren(add);
        }

        protected abstract void OnInitialize();
        protected abstract void OnBuildChildren(System.Action<BuildChildrenArgs> add);

        public override void OnGUI(Rect position, int column)
        {
            position.width -= 2;
            position.x += 1;
            position.height -= 2;
            position.y += 1;

            //var oldcolor = GUI.color;
            //if (!enabled)
            //    GUI.color = new Color(oldcolor.r, oldcolor.g, oldcolor.b, oldcolor.a * 0.5f);

            if (column == 0 && icon != null)
            {
                GUI.Box(HeEditorGUI.SpaceL(ref position, position.height), icon, HeEditorStyles.iconStyle);
            }

            switch ((PropertyGridColumn)column)
            {
                case PropertyGridColumn.Type:
                    GUI.Label(position, displayType);
                    break;

                case PropertyGridColumn.Name:
                    TryDrawObjectButton(this, ref position);
                    GUI.Label(position, new GUIContent(displayName, tooltip));
                    break;

                case PropertyGridColumn.Value:
                    TryDrawDataVisualizerButton(this, ref position);
                    GUI.Label(position, displayValue);
                    break;
            }

            //if (!enabled)
            //    GUI.color = oldcolor;
        }

        void TryDrawDataVisualizerButton(PropertyGridItem itm, ref Rect rect)
        {
            if (!enabled || typeIndex == -1)
                return;

            if (!AbstractDataVisualizer.HasVisualizer(m_type.name))
                return;

            if (GUI.Button(HeEditorGUI.SpaceR(ref rect, rect.height), new GUIContent("", "Show in Data Visualizer."), HeEditorStyles.dataVisualizer))
            {
                var pointer = address;
                if (m_type.isPointer)
                    pointer = myMemoryReader.ReadPointer(address);

                DataVisualizerWindow.CreateWindow(m_snapshot, myMemoryReader, pointer, m_type);
            }
        }

        void TryDrawObjectButton(PropertyGridItem itm, ref Rect rect)
        {
            // We do not test against address==0 here, because if it is a static field its address might be 0
            if (itm.typeIndex < 0 || (itm is BaseClassPropertyGridItem))
                return;

            // If it is not a pointer, it does not point to another object
            //var type = m_snapshot.managedTypes[itm.typeIndex];
            if (!itm.m_type.isPointer)
                return;

            // If it points to null, it has no object
            var pointer = itm.myMemoryReader.ReadPointer(itm.address);
            if (pointer == 0)
                return;

            // Check if it is a managed object
            var managedObjIndex = m_snapshot.FindManagedObjectOfAddress(itm.m_type.isArray ? itm.address : pointer);
            if (managedObjIndex != -1)
            {
                if (HeEditorGUI.CsButton(HeEditorGUI.SpaceR(ref rect, rect.height)))
                {
                    m_owner.m_Window.OnGoto(new GotoCommand(new RichManagedObject(m_snapshot, managedObjIndex)));
                }
            }

            // Check if it is a native object
            var nativeObjIndex = m_snapshot.FindNativeObjectOfAddress(pointer);
            if (nativeObjIndex != -1)
            {
                if (HeEditorGUI.CppButton(HeEditorGUI.SpaceR(ref rect, rect.height)))
                {
                    m_owner.m_Window.OnGoto(new GotoCommand(new RichNativeObject(m_snapshot, nativeObjIndex)));
                }
            }
        }
    }
}
