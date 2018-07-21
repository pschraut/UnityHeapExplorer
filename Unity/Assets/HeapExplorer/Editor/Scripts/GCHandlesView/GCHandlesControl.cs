using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;

namespace HeapExplorer
{
    public class GCHandlesControl : AbstractTreeView
    {
        //public System.Action<GotoCommand> gotoCB;
        public System.Action<PackedGCHandle?> onSelectionChange;

        PackedMemorySnapshot m_snapshot;
        int m_uniqueId = 1;

        enum Column
        {
            GCHandle,
            Size,
            Count,
            Address,
        }

        public GCHandlesControl(HeapExplorerWindow window, string editorPrefsKey, TreeViewState state)
            : base(window, editorPrefsKey, state, new MultiColumnHeader(
                new MultiColumnHeaderState(new[]
                {
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("GCHandle"), width = 150, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Size"), width = 80, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Count"), width = 80, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Address"), width = 120, autoResize = true },
                })))
        {
            multiColumnHeader.canSort = true;
            //multiColumnHeader.sortingChanged += OnSortingChanged;

            Reload();
        }
        
        public void Select(PackedGCHandle obj)
        {
            var item = FindItemByAddressRecursive(rootItem, (ulong)(obj.target));
            if (item != null)
                SetSelection(new[] { item.id }, TreeViewSelectionOptions.RevealAndFrame | TreeViewSelectionOptions.FireSelectionChanged);
        }

        TreeViewItem FindItemByAddressRecursive(TreeViewItem parent, System.UInt64 address)
        {
            if (parent != null)
            {
                var item = parent as AbstractItem;
                if (item != null && item.address == address)
                    return item;

                if (parent.hasChildren)
                {
                    for (int n = 0, nend = parent.children.Count; n < nend; ++n)
                    {
                        var child = parent.children[n];

                        var value = FindItemByAddressRecursive(child, address);
                        if (value != null)
                            return value;
                    }
                }
            }

            return null;
        }

        //void OnSortingChanged(MultiColumnHeader multiColumnHeader)
        //{
        //    SetTree(BuildTree(m_snapshot));
        //}

        protected override void OnSelectionChanged(TreeViewItem selectedItem)
        {
            base.OnSelectionChanged(selectedItem);

            if (onSelectionChange == null)
                return;

            var item = selectedItem as GCHandleItem;
            if (item == null)
            {
                onSelectionChange.Invoke(null);
                return;
            }

            onSelectionChange.Invoke(item.packed);
        }

        public TreeViewItem BuildTree(PackedMemorySnapshot snapshot)
        {
            m_snapshot = snapshot;
            m_uniqueId = 1;

            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
            if (m_snapshot == null)
            {
                root.AddChild(new TreeViewItem { id = 1, depth = -1, displayName = "" });
                return root;
            }

            // int=typeIndex
            var groupLookup = new Dictionary<int, GroupItem>();

            for (int n = 0, nend = m_snapshot.gcHandles.Length; n < nend; ++n)
            {
                var gcHandle = m_snapshot.gcHandles[n];
                var managedTypeIndex = -1;
                if (gcHandle.managedObjectsArrayIndex >= 0)
                    managedTypeIndex = m_snapshot.managedObjects[gcHandle.managedObjectsArrayIndex].managedTypesArrayIndex;

                var targetItem = root;
                if (managedTypeIndex >= 0)
                {
                    GroupItem group;
                    if (!groupLookup.TryGetValue(managedTypeIndex, out group))
                    {
                        group = new GroupItem
                        {
                            id = m_uniqueId++,
                            depth = 0,
                            displayName = ""
                        };
                        group.Initialize(m_snapshot, managedTypeIndex);

                        groupLookup[managedTypeIndex] = group;
                        root.AddChild(group);
                    }

                    targetItem = group;
                }

                var item = new GCHandleItem
                {
                    id = m_uniqueId++,
                    depth = targetItem.depth + 1,
                    displayName = ""
                };
                item.Initialize(this, m_snapshot, gcHandle.gcHandlesArrayIndex);

                targetItem.AddChild(item);
            }

            // remove groups if it contains one item only
            for (int n = root.children.Count - 1; n >= 0; --n)
            {
                var group = root.children[n];
                if (group.hasChildren && group.children.Count == 1)
                {
                    group.children[0].depth -= 1;
                    root.AddChild(group.children[0]);
                    root.children.RemoveAt(n);
                }
            }

            SortItemsRecursive(root, OnSortItem);

            return root;
        }
        
        protected override int OnSortItem(TreeViewItem aa, TreeViewItem bb)
        {
            var sortingColumn = multiColumnHeader.sortedColumnIndex;
            var ascending = multiColumnHeader.IsSortedAscending(sortingColumn);

            var itemA = (ascending ? aa : bb) as AbstractItem;
            var itemB = (ascending ? bb : aa) as AbstractItem;

            switch ((Column)sortingColumn)
            {
                case Column.GCHandle:
                    return string.Compare(itemB.typeName, itemA.typeName, true);

                case Column.Size:
                    return itemA.size.CompareTo(itemB.size);

                case Column.Count:
                    return itemA.count.CompareTo(itemB.count);
            }

            return 0;
        }

        ///////////////////////////////////////////////////////////////////////////

        abstract class AbstractItem : AbstractTreeViewItem
        {
            public abstract string typeName
            {
                get;
            }

            public abstract int size
            {
                get;
            }


            public abstract int count
            {
                get;
            }

            public abstract System.UInt64 address
            {
                get;
            }
        }

        ///////////////////////////////////////////////////////////////////////////

        class GCHandleItem : AbstractItem
        {
            RichGCHandle m_gcHandle;
            GCHandlesControl m_owner;

            public PackedGCHandle packed
            {
                get
                {
                    return m_gcHandle.packed;
                }
            }

            public override string typeName
            {
                get
                {
                    return m_gcHandle.managedObject.type.name;
                }
            }

            public override int size
            {
                get
                {
                    return m_gcHandle.size;
                }
            }

            public override int count
            {
                get
                {
                    return 0;
                }
            }

            public override System.UInt64 address
            {
                get
                {
                    return m_gcHandle.managedObjectAddress;
                }
            }

            public void Initialize(GCHandlesControl owner, PackedMemorySnapshot snapshot, int gcHandlesArrayIndex)
            {
                m_owner = owner;
                m_gcHandle = new RichGCHandle(snapshot, gcHandlesArrayIndex);
            }

            public override void GetItemSearchString(string[] target, out int count)
            {
                count = 0;
                target[count++] = typeName;
                target[count++] = string.Format(StringFormat.Address, address);
            }
            
            public override void OnGUI(Rect position, int column)
            {
                if (column == 0)
                {
                    GUI.Box(HeEditorGUI.SpaceL(ref position, position.height), HeEditorStyles.gcHandleImage, HeEditorStyles.iconStyle);

                    if (m_gcHandle.nativeObject.isValid)
                    {
                        if (HeEditorGUI.CppButton(HeEditorGUI.SpaceR(ref position, position.height)))
                        {
                            m_owner.window.OnGoto(new GotoCommand(m_gcHandle.nativeObject));
                        }
                    }

                    if (m_gcHandle.managedObject.isValid)
                    {
                        if (HeEditorGUI.CsButton(HeEditorGUI.SpaceR(ref position, position.height)))
                        {
                            m_owner.window.OnGoto(new GotoCommand(m_gcHandle.managedObject));
                        }
                    }
                }

                switch ((Column)column)
                {
                    case Column.GCHandle:
                        HeEditorGUI.TypeName(position, typeName);
                        break;

                    case Column.Size:
                        HeEditorGUI.Size(position, size);
                        break;

                    case Column.Address:
                        HeEditorGUI.Address(position, address);
                        break;
                }
            }
        }


        ///////////////////////////////////////////////////////////////////////////

        class GroupItem : AbstractItem
        {
            int m_size = -1;
            RichManagedType m_type;           

            public override string typeName
            {
                get
                {
                    return m_type.name;
                }
            }

            public override int size
            {
                get
                {
                    if (m_size == -1)
                    {
                        m_size = 0;
                        for (int n = 0, nend = children.Count; n < nend; ++n)
                        {
                            var child = children[n] as AbstractItem;
                            if (child != null)
                                m_size += child.size;
                        }
                    }

                    return m_size;
                }
            }

            public override int count
            {
                get
                {
                    return children.Count;
                }
            }

            public override System.UInt64 address
            {
                get
                {
                    return 0;
                }
            }

            public void Initialize(PackedMemorySnapshot snapshot, int managedTypesArrayIndex)
            {
                m_type = new RichManagedType(snapshot, managedTypesArrayIndex);
            }
            
            public override void OnGUI(Rect position, int column)
            {
                switch ((Column)column)
                {
                    case Column.GCHandle:
                        HeEditorGUI.TypeName(position, typeName);
                        break;

                    case Column.Size:
                        HeEditorGUI.Size(position, size);
                        break;

                    case Column.Count:
                        GUI.Label(position, count.ToString());
                        break;
                }
            }
        }
    }
}
