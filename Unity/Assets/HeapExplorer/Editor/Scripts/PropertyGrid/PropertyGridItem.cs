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
    enum PropertyGridColumn
    {
        Name,
        Value,
        Type
    }

    abstract public class PropertyGridItem : AbstractTreeViewItem
    {
        public System.UInt64 address;
        public bool isExpandable;
        public string displayType = "";
        public string displayValue = "";
        public string tooltip = "";
        public PackedManagedType type;

        public AbstractMemoryReader myMemoryReader
        {
            get
            {
                return m_MemoryReader;
            }
        }

        protected AbstractMemoryReader m_MemoryReader;
        protected PackedMemorySnapshot m_Snapshot;
        protected PropertyGridControl m_Owner;

        bool m_IsInitialized;
        bool m_HasBuiltChildren;

        static int s_UniqueId = 1;

        public PropertyGridItem(PropertyGridControl owner, PackedMemorySnapshot snapshot, System.UInt64 address, AbstractMemoryReader memoryReader)
            : base()
        {
            m_Owner = owner;

            if (children == null)
                children = new List<TreeViewItem>();

            base.id = s_UniqueId++;
            this.m_Snapshot = snapshot;
            this.address = address;
            this.m_MemoryReader = memoryReader;
            this.tooltip = this.GetType().Name;
        }

        public void Initialize()
        {
            if (m_IsInitialized)
                return;
            m_IsInitialized = true;

            OnInitialize();

            var text = string.Format("{0} {1} = {2}\n\n{3}", 
                displayType, 
                displayName,
                displayValue,
                PackedManagedTypeUtility.GetInheritanceAsString(m_Snapshot, type.managedTypesArrayIndex));

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
            if (m_HasBuiltChildren)
                return;
            m_HasBuiltChildren = true;

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
            if (!enabled || type.managedTypesArrayIndex == -1)
                return;

            if (!AbstractDataVisualizer.HasVisualizer(type.name))
                return;

            if (GUI.Button(HeEditorGUI.SpaceR(ref rect, rect.height), new GUIContent("", "Show in Data Visualizer."), HeEditorStyles.dataVisualizer))
            {
                var pointer = address;
                if (type.isPointer)
                    pointer = myMemoryReader.ReadPointer(address);

                DataVisualizerWindow.CreateWindow(m_Snapshot, myMemoryReader, pointer, type);
            }
        }

        void TryDrawObjectButton(PropertyGridItem itm, ref Rect rect)
        {
            // We do not test against address==0 here, because if it is a static field its address might be 0
            if (itm.type.managedTypesArrayIndex < 0 || (itm is BaseClassPropertyGridItem))
                return;

            // If it is not a pointer, it does not point to another object
            //var type = m_snapshot.managedTypes[itm.typeIndex];
            if (!itm.type.isPointer)
                return;

            // If it points to null, it has no object
            var pointer = itm.myMemoryReader.ReadPointer(itm.address);
            if (pointer == 0)
                return;

            // Check if it is a managed object
            var managedObjIndex = m_Snapshot.FindManagedObjectOfAddress(itm.type.isArray ? itm.address : pointer);
            if (managedObjIndex != -1)
            {
                if (HeEditorGUI.CsButton(HeEditorGUI.SpaceR(ref rect, rect.height)))
                {
                    m_Owner.window.OnGoto(new GotoCommand(new RichManagedObject(m_Snapshot, managedObjIndex)));
                }
            }

            // Check if it is a native object
            var nativeObjIndex = m_Snapshot.FindNativeObjectOfAddress(pointer);
            if (nativeObjIndex != -1)
            {
                if (HeEditorGUI.CppButton(HeEditorGUI.SpaceR(ref rect, rect.height)))
                {
                    m_Owner.window.OnGoto(new GotoCommand(new RichNativeObject(m_Snapshot, nativeObjIndex)));
                }
            }
        }
    }
}
