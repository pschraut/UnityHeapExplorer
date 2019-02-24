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
    public class AbstractManagedObjectsControl : AbstractTreeView
    {
        public System.Action<PackedManagedObject?> onSelectionChange;

        /// <summary>
        /// Gets the number of all managed objects in the tree.
        /// </summary>
        public long managedObjectsCount
        {
            get
            {
                return m_ManagedObjectCount;
            }
        }

        /// <summary>
        /// Gets the size in bytes of all managed objects in the tree.
        /// </summary>
        public long managedObjectsSize
        {
            get
            {
                return m_ManagedObjectSize;
            }
        }

        protected PackedMemorySnapshot m_Snapshot;
        protected int m_UniqueId = 1;
        protected long m_ManagedObjectCount;
        protected long m_ManagedObjectSize;

        enum Column
        {
            Type,
            CppCounterpart,
            Size,
            Count,
            Address,
            Assembly
        }

        public AbstractManagedObjectsControl(HeapExplorerWindow window, string editorPrefsKey, TreeViewState state)
            : base(window, editorPrefsKey, state, new MultiColumnHeader(
                new MultiColumnHeaderState(new[]
                {
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("C# Type"), width = 250, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("C++ Name", "If the C# object has a C++ counterpart, display its C++ object name in this column."), width = 150, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Size"), width = 80, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Count"), width = 80, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Address"), width = 120, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Assembly"), width = 120, autoResize = true },
                })))
        {
            multiColumnHeader.canSort = true;

            Reload();
        }

        public void Select(PackedManagedObject obj)
        {
            var item = FindItemByAddressRecursive(rootItem, (ulong)(obj.address));
            SelectItem(item);
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

            var item = selectedItem as ManagedObjectItem;
            if (item == null)
            {
                onSelectionChange.Invoke(null);
                return;
            }

            onSelectionChange.Invoke(item.managedObject.packed);
        }

        public TreeViewItem BuildTree(PackedMemorySnapshot snapshot)
        {
            m_Snapshot = snapshot;
            m_UniqueId = 1;
            m_ManagedObjectCount = 0;
            m_ManagedObjectSize = 0;

            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };

            if (m_Snapshot == null || m_Snapshot.managedObjects == null || m_Snapshot.managedObjects.Length == 0)
            {
                root.AddChild(new TreeViewItem { id = 1, depth = -1, displayName = "" });
                return root;
            }

            OnBeforeBuildTree();
            OnBuildTree(root);

            // remove groups if it contains one item only
            if (root.hasChildren)
            {
                for (int n = root.children.Count - 1; n >= 0; --n)
                {
                    var group = root.children[n];
                    if (group.children.Count == 1)
                    {
                        group.children[0].depth -= 1;
                        root.AddChild(group.children[0]);
                        root.children.RemoveAt(n);
                        continue;
                    }
                }
            }

            SortItemsRecursive(root, OnSortItem);

            return root;
        }

        protected virtual bool OnCanAddObject(PackedManagedObject mo)
        {
            return true;
        }

        protected virtual void OnBeforeBuildTree()
        {
        }

        protected virtual void OnBuildTree(TreeViewItem root)
        {
            // int=typeIndex
            var groupLookup = new Dictionary<int, GroupItem>();

            for (int n = 0, nend = m_Snapshot.managedObjects.Length; n < nend; ++n)
            {
                var mo = m_Snapshot.managedObjects[n];
                if (!OnCanAddObject(mo))
                    continue;

                GroupItem group;
                if (!groupLookup.TryGetValue(mo.managedTypesArrayIndex, out group))
                {
                    group = new GroupItem
                    {
                        id = m_UniqueId++,
                        depth = 0,
                        displayName = ""
                    };
                    group.Initialize(m_Snapshot, m_Snapshot.managedTypes[mo.managedTypesArrayIndex]);

                    groupLookup[mo.managedTypesArrayIndex] = group;
                    root.AddChild(group);
                }

                var item = new ManagedObjectItem
                {
                    id = m_UniqueId++,
                    depth = group.depth + 1,
                    displayName = ""
                };
                item.Initialize(this, m_Snapshot, mo);

                group.AddChild(item);

                m_ManagedObjectCount++;
                m_ManagedObjectSize += item.size;
            }
        }

        protected override int OnSortItem(TreeViewItem aa, TreeViewItem bb)
        {
            var sortingColumn = multiColumnHeader.sortedColumnIndex;
            var ascending = multiColumnHeader.IsSortedAscending(sortingColumn);

            var itemA = (ascending ? aa : bb) as AbstractItem;
            var itemB = (ascending ? bb : aa) as AbstractItem;

            switch ((Column)sortingColumn)
            {
                case Column.Type:
                    return string.Compare(itemB.typeName, itemA.typeName, true);

                case Column.CppCounterpart:
                    return string.Compare(itemB.cppName, itemA.cppName, true);

                case Column.Size:
                    return itemA.size.CompareTo(itemB.size);

                case Column.Count:
                    return itemA.count.CompareTo(itemB.count);

                case Column.Address:
                    return itemA.address.CompareTo(itemB.address);

                case Column.Assembly:
                    return string.Compare(itemB.assembly, itemA.assembly, true);
            }

            return 0;
        }

        ///////////////////////////////////////////////////////////////////////////

        public abstract class AbstractItem : AbstractTreeViewItem
        {
            public abstract string typeName
            {
                get;
            }

            public abstract string cppName
            {
                get;
            }

            public abstract long size
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

            public abstract string assembly
            {
                get;
            }
        }

        ///////////////////////////////////////////////////////////////////////////

        public class ManagedObjectItem : AbstractItem
        {
            RichManagedObject m_Object;
            AbstractManagedObjectsControl m_Owner;

            public RichManagedObject managedObject
            {
                get
                {
                    return m_Object;
                }
            }

            public override string typeName
            {
                get
                {
                    return m_Object.type.name;
                }
            }

            public override string assembly
            {
                get
                {
                    return m_Object.type.assemblyName;
                }
            }

            public override string cppName
            {
                get
                {
                    var nativeObj = m_Object.nativeObject;
                    if (nativeObj.isValid)
                        return nativeObj.name;

                    return "";
                }
            }

            public override long size
            {
                get
                {
                    return m_Object.size;
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
                    return m_Object.address;
                }
            }

            public void Initialize(AbstractManagedObjectsControl owner, PackedMemorySnapshot snapshot, PackedManagedObject managedObject)
            {
                m_Owner = owner;

                m_Object = new RichManagedObject(snapshot, managedObject.managedObjectsArrayIndex);
            }

            public override void GetItemSearchString(string[] target, out int count)
            {
                count = 0;
                target[count++] = typeName;
                target[count++] = string.Format(StringFormat.Address, m_Object.address);
                target[count++] = cppName;
                target[count++] = assembly;
            }

            public override void OnGUI(Rect position, int column)
            {
                if (column == 0)
                {
                    GUI.Box(HeEditorGUI.SpaceL(ref position, position.height), HeEditorStyles.csImage, HeEditorStyles.iconStyle);

                    //if (m_managedObject.gcHandlesArrayIndex != -1)
                    //{
                    //    if (HeEditorGUI.GCHandleButton(HeEditorGUI.SpaceR(ref position, position.height)))
                    //    {
                    //        m_owner.m_Window.OnGoto(new GotoCommand(new RichGCHandle(m_snapshot, m_snapshot.gcHandles[m_managedObject.gcHandlesArrayIndex])));
                    //    }
                    //}

                    if (m_Object.nativeObject.isValid)
                    {
                        if (HeEditorGUI.CppButton(HeEditorGUI.SpaceR(ref position, position.height)))
                        {
                            m_Owner.window.OnGoto(new GotoCommand(m_Object.nativeObject));
                        }
                    }
                }

                switch ((Column)column)
                {
                    case Column.Type:
                        HeEditorGUI.TypeName(position, typeName);
                        break;

                    case Column.CppCounterpart:
                        if (m_Object.nativeObject.isValid)
                            GUI.Label(position, m_Object.nativeObject.name);
                        break;

                    case Column.Size:
                        HeEditorGUI.Size(position, size);
                        break;

                    case Column.Address:
                        HeEditorGUI.Address(position, m_Object.address);
                        break;

                    case Column.Assembly:
                        GUI.Label(position, assembly);
                        break;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////////

        public class GroupItem : AbstractItem
        {
            RichManagedType m_Type;
            long m_Size = -1;

            public override string typeName
            {
                get
                {
                    return m_Type.name;
                }
            }

            public override string assembly
            {
                get
                {
                    return m_Type.assemblyName;
                }
            }

            public override string cppName
            {
                get
                {
                    return "";
                }
            }

            public override long size
            {
                get
                {
                    if (m_Size == -1)
                    {
                        m_Size = 0;
                        if (hasChildren)
                        {
                            for (int n = 0, nend = children.Count; n < nend; ++n)
                            {
                                var child = children[n] as AbstractItem;
                                if (child != null)
                                    m_Size += child.size;
                            }
                        }
                    }
                    return m_Size;
                }
            }

            public override int count
            {
                get
                {
                    if (hasChildren)
                        return children.Count;
                    return 0;
                }
            }

            public override System.UInt64 address
            {
                get
                {
                    return 0;
                }
            }

            public void Initialize(PackedMemorySnapshot snapshot, PackedManagedType type)
            {
                m_Type = new RichManagedType(snapshot, type.managedTypesArrayIndex);
            }

            public override void OnGUI(Rect position, int column)
            {
                switch ((Column)column)
                {
                    case Column.Type:
                        HeEditorGUI.TypeName(position, typeName);
                        break;

                    case Column.Size:
                        HeEditorGUI.Size(position, size);
                        break;

                    case Column.Count:
                        GUI.Label(position, count.ToString());
                        break;

                    case Column.Assembly:
                        GUI.Label(position, assembly);
                        break;
                }
            }
        }
    }
}
