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
    /// <summary>
    /// The NativeObjectControl class provides the ability to display properties of a native UnityEngine object,
    /// such as a Mesh or MonoScript.
    /// </summary>
    sealed public class NativeObjectControl : AbstractTreeView
    {
        PackedMemorySnapshot m_Snapshot;
        PackedNativeUnityEngineObject m_Object;
        int m_UniqueId = 1;

        public NativeObjectControl(HeapExplorerWindow window, string editorPrefsKey, TreeViewState state)
            : base(window, editorPrefsKey, state, new MultiColumnHeader(
                new MultiColumnHeaderState(new[]
                {
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Name"), width = 200 },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Value"), width = 200 }
                })))
        {
            multiColumnHeader.canSort = false;

            Reload();
        }

        public void Inspect(PackedMemorySnapshot snapshot, PackedNativeUnityEngineObject nativeObject)
        {
            m_Snapshot = snapshot;
            m_Object = nativeObject;

            Reload();
        }

        public void Clear()
        {
            m_Snapshot = null;
            Reload();
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
            if (m_Snapshot == null)
            {
                root.AddChild(new TreeViewItem { id = 1, depth = -1, displayName = "" });
                return root;
            }

            AddTreeViewItem(root, new Item() { displayName = "Name", value = m_Object.name });
            AddTreeViewItem(root, new Item() { displayName = "Type", value = m_Snapshot.nativeTypes[m_Object.nativeTypesArrayIndex].name });
            AddTreeViewItem(root, new Item() { displayName = "Size", value = EditorUtility.FormatBytes(m_Object.size) });
            AddTreeViewItem(root, new Item() { displayName = "Address", value = string.Format(StringFormat.Address, m_Object.nativeObjectAddress) });
            AddTreeViewItem(root, new Item() { displayName = "InstanceID", value = m_Object.instanceId.ToString() });
            AddTreeViewItem(root, new Item() { displayName = "Persistent", value = m_Object.isPersistent.ToString() });
            AddTreeViewItem(root, new Item() { displayName = "DontDestroyOnLoad", value = m_Object.isDontDestroyOnLoad.ToString() });
            AddTreeViewItem(root, new Item() { displayName = "IsManager", value = m_Object.isManager.ToString() });
            AddTreeViewItem(root, new Item() { displayName = "HideFlags", value = m_Object.hideFlags.ToString() });

            return root;
        }

        protected override int OnSortItem(TreeViewItem x, TreeViewItem y)
        {
            return 0;
        }

        void AddTreeViewItem(TreeViewItem parent, TreeViewItem item)
        {
            item.id = m_UniqueId++;
            item.depth = parent.depth + 1;
            parent.AddChild(item);
        }

        class Item : AbstractTreeViewItem
        {
            public string value;
            
            public override void OnGUI(Rect position, int column)
            {
                switch (column)
                {
                    case 0:
                        EditorGUI.LabelField(position, displayName);
                        break;
                    case 1:
                        EditorGUI.LabelField(position, value);
                        break;
                }
            }
        }
    }
}
