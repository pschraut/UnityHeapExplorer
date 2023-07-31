﻿//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//

using System;
using System.Collections;
using System.Collections.Generic;
using HeapExplorer.Utilities;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;
using static HeapExplorer.Utilities.Option;

namespace HeapExplorer
{
    public class GCHandlesControl : AbstractTreeView
    {
        public System.Action<Option<PackedGCHandle>> onSelectionChange;

        PackedMemorySnapshot m_Snapshot;
        int m_UniqueId = 1;

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

            Reload();
        }

        public void Select(PackedGCHandle obj)
        {
            var item = FindItemByAddressRecursive(rootItem, obj.target);
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

        protected override void OnSelectionChanged(TreeViewItem selectedItem)
        {
            base.OnSelectionChanged(selectedItem);

            if (onSelectionChange == null)
                return;

            var item = selectedItem as GCHandleItem;
            if (item == null)
            {
                onSelectionChange.Invoke(None._);
                return;
            }

            onSelectionChange.Invoke(Some(item.packed));
        }

        public TreeViewItem BuildTree(PackedMemorySnapshot snapshot)
        {
            m_Snapshot = snapshot;
            m_UniqueId = 1;

            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
            if (m_Snapshot == null)
            {
                root.AddChild(new TreeViewItem { id = 1, depth = -1, displayName = "" });
                return root;
            }

            // int=typeIndex
            var groupLookup = new Dictionary<int, GroupItem>();

            for (int n = 0, nend = m_Snapshot.gcHandles.Length; n < nend; ++n)
            {
                if (window.isClosing) // the window is closing
                    break;

                var gcHandle = m_Snapshot.gcHandles[n];
                var maybeManagedTypeIndex = gcHandle.managedObjectsArrayIndex.map(m_Snapshot, (idx, ss) =>
                    idx.isStatic
                        ? ss.managedStaticFields[idx.index].managedTypesArrayIndex
                        : ss.managedObjects[idx.index].managedTypesArrayIndex
                );

                var targetItem = root;
                {if (maybeManagedTypeIndex.valueOut(out var managedTypeIndex)) {
                    if (!groupLookup.TryGetValue(managedTypeIndex, out var group)) {
                        group = new GroupItem
                        {
                            id = m_UniqueId++,
                            depth = 0,
                            displayName = ""
                        };
                        group.Initialize(m_Snapshot, managedTypeIndex);

                        groupLookup[managedTypeIndex] = group;
                        root.AddChild(group);
                    }

                    targetItem = group;
                }}

                var item = new GCHandleItem
                {
                    id = m_UniqueId++,
                    depth = targetItem.depth + 1,
                    displayName = ""
                };
                item.Initialize(this, m_Snapshot, gcHandle.gcHandlesArrayIndex);

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
                    return string.Compare(itemB.typeName, itemA.typeName, StringComparison.OrdinalIgnoreCase);

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
            public PackedGCHandle packed
            {
                get
                {
                    return m_GCHandle.packed;
                }
            }

            public override string typeName
            {
                get
                {
                    return m_GCHandle.managedObject.fold("broken handle", _ => _.type.name);
                }
            }

            public override int size
            {
                get
                {
                    return m_GCHandle.size;
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
                    return m_GCHandle.managedObjectAddress;
                }
            }

            RichGCHandle m_GCHandle;
            GCHandlesControl m_Owner;

            public void Initialize(GCHandlesControl owner, PackedMemorySnapshot snapshot, int gcHandlesArrayIndex)
            {
                m_Owner = owner;
                m_GCHandle = new RichGCHandle(snapshot, gcHandlesArrayIndex);
            }

            public override void GetItemSearchString(string[] target, out int count, out string type, out string label)
            {
                base.GetItemSearchString(target, out count, out type, out label);

                type = typeName;
                target[count++] = typeName;
                target[count++] = string.Format(StringFormat.Address, address);
            }

            public override void OnGUI(Rect position, int column)
            {
                if (column == 0)
                {
                    GUI.Box(HeEditorGUI.SpaceL(ref position, position.height), HeEditorStyles.gcHandleImage, HeEditorStyles.iconStyle);

                    {if (m_GCHandle.nativeObject.valueOut(out var nativeObject)) {
                        if (HeEditorGUI.CppButton(HeEditorGUI.SpaceR(ref position, position.height)))
                        {
                            m_Owner.window.OnGoto(new GotoCommand(nativeObject));
                        }
                    }}

                    {if (m_GCHandle.managedObject.valueOut(out var managedObject)) {
                        if (HeEditorGUI.CsButton(HeEditorGUI.SpaceR(ref position, position.height)))
                        {
                            m_Owner.window.OnGoto(new GotoCommand(managedObject));
                        }
                    }}
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
            public override string typeName
            {
                get
                {
                    return m_Type.name;
                }
            }

            public override int size
            {
                get
                {
                    if (m_Size == -1)
                    {
                        m_Size = 0;
                        for (int n = 0, nend = children.Count; n < nend; ++n)
                        {
                            var child = children[n] as AbstractItem;
                            if (child != null)
                                m_Size += child.size;
                        }
                    }

                    return m_Size;
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

            int m_Size = -1;
            RichManagedType m_Type;

            public void Initialize(PackedMemorySnapshot snapshot, int managedTypesArrayIndex)
            {
                m_Type = new RichManagedType(snapshot, managedTypesArrayIndex);
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
