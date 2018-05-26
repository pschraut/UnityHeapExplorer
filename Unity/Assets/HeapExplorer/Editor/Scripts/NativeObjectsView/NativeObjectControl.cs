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
        PackedMemorySnapshot m_snapshot;
        PackedNativeUnityEngineObject m_object;
        int m_uniqueId = 1;

        public NativeObjectControl(string editorPrefsKey, TreeViewState state)
            : base(editorPrefsKey, state, new MultiColumnHeader(
                new MultiColumnHeaderState(new[]
                {
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Name"), width = 200 },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Value"), width = 200 }
                })))
        {
            //rowHeight = 20;
            //showAlternatingRowBackgrounds = true;
            //showBorder = false;
            //extraSpaceBeforeIconAndLabel = 4;
            //baseIndent = 4;
            //columnIndexForTreeFoldouts = 0;
            //extraSpaceBeforeIconAndLabel = 16;

            multiColumnHeader.canSort = false;

            Reload();
        }

        public void Inspect(PackedMemorySnapshot snapshot, PackedNativeUnityEngineObject nativeObject)
        {
            m_snapshot = snapshot;
            m_object = nativeObject;

            Reload();
        }

        public void Clear()
        {
            m_snapshot = null;
            Reload();
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
            if (m_snapshot == null)
            {
                root.AddChild(new TreeViewItem { id = 1, depth = -1, displayName = "" });
                return root;
            }

            AddTreeViewItem(root, new Item() { displayName = "Name", value = m_object.name });
            AddTreeViewItem(root, new Item() { displayName = "Type", value = m_snapshot.nativeTypes[m_object.nativeTypesArrayIndex].name });
            AddTreeViewItem(root, new Item() { displayName = "Size", value = EditorUtility.FormatBytes(m_object.size) });
            AddTreeViewItem(root, new Item() { displayName = "Address", value = string.Format(StringFormat.Address, m_object.nativeObjectAddress) });
            AddTreeViewItem(root, new Item() { displayName = "InstanceID", value = m_object.instanceId.ToString() });
            AddTreeViewItem(root, new Item() { displayName = "Persistent", value = m_object.isPersistent.ToString() });
            AddTreeViewItem(root, new Item() { displayName = "DontDestroyOnLoad", value = m_object.isDontDestroyOnLoad.ToString() });
            AddTreeViewItem(root, new Item() { displayName = "IsManager", value = m_object.isManager.ToString() });
            AddTreeViewItem(root, new Item() { displayName = "HideFlags", value = m_object.hideFlags.ToString() });

            return root;
        }

        protected override int OnSortItem(TreeViewItem x, TreeViewItem y)
        {
            return 0;
        }

        void AddTreeViewItem(TreeViewItem parent, TreeViewItem item)
        {
            item.id = m_uniqueId++;
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
