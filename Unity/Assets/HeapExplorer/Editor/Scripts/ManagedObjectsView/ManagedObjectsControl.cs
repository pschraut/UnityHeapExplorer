using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;

namespace HeapExplorer
{
    public class AbstractManagedObjectsControl : AbstractTreeView
    {
        public System.Action<GotoCommand> gotoCB;
        public System.Action<PackedManagedObject?> onSelectionChange;

        /// <summary>
        /// Gets the number of all managed objects in the tree.
        /// </summary>
        public int managedObjectsCount
        {
            get
            {
                return m_managedObjectCount;
            }
        }

        /// <summary>
        /// Gets the size in bytes of all managed objects in the tree.
        /// </summary>
        public int managedObjectsSize
        {
            get
            {
                return m_managedObjectSize;
            }
        }

        protected PackedMemorySnapshot m_snapshot;
        protected int m_uniqueId = 1;
        protected int m_managedObjectCount;
        protected int m_managedObjectSize;

        enum Column
        {
            Type,
            CppCounterpart,
            Size,
            Count,
            Address,
            Assembly
        }

        public AbstractManagedObjectsControl(string editorPrefsKey, TreeViewState state)
            : base(editorPrefsKey, state, new MultiColumnHeader(
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
            m_snapshot = snapshot;
            m_uniqueId = 1;
            m_managedObjectCount = 0;
            m_managedObjectSize = 0;

            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };

            if (m_snapshot == null || m_snapshot.managedObjects == null || m_snapshot.managedObjects.Length == 0)
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

            for (int n = 0, nend = m_snapshot.managedObjects.Length; n < nend; ++n)
            {
                var mo = m_snapshot.managedObjects[n];
                if (!OnCanAddObject(mo))
                    continue;

                GroupItem group;
                if (!groupLookup.TryGetValue(mo.managedTypesArrayIndex, out group))
                {
                    group = new GroupItem
                    {
                        id = m_uniqueId++,
                        depth = 0,
                        displayName = ""
                    };
                    group.Initialize(m_snapshot, m_snapshot.managedTypes[mo.managedTypesArrayIndex]);

                    groupLookup[mo.managedTypesArrayIndex] = group;
                    root.AddChild(group);
                }

                var item = new ManagedObjectItem
                {
                    id = m_uniqueId++,
                    depth = group.depth + 1,
                    displayName = ""
                };
                item.Initialize(this, m_snapshot, mo);

                group.AddChild(item);

                m_managedObjectCount++;
                m_managedObjectSize += item.size;
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

            public abstract string assembly
            {
                get;
            }
        }

        ///////////////////////////////////////////////////////////////////////////

        public class ManagedObjectItem : AbstractItem
        {
            RichManagedObject m_object;
            AbstractManagedObjectsControl m_owner;

            public RichManagedObject managedObject
            {
                get
                {
                    return m_object;
                }
            }

            public override string typeName
            {
                get
                {
                    return m_object.type.name;
                }
            }

            public override string assembly
            {
                get
                {
                    return m_object.type.assemblyName;
                }
            }

            public override string cppName
            {
                get
                {
                    var nativeObj = m_object.nativeObject;
                    if (nativeObj.isValid)
                        return nativeObj.name;

                    return "";
                }
            }

            public override int size
            {
                get
                {
                    return m_object.size;
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
                    return m_object.address;
                }
            }

            public void Initialize(AbstractManagedObjectsControl owner, PackedMemorySnapshot snapshot, PackedManagedObject managedObject)
            {
                m_owner = owner;

                m_object = new RichManagedObject(snapshot, managedObject.managedObjectsArrayIndex);
            }

            public override void GetItemSearchString(string[] target, out int count)
            {
                count = 0;
                target[count++] = typeName;
                target[count++] = string.Format(StringFormat.Address, m_object.address);
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
                    //        m_owner.gotoCB(new GotoCommand(new RichGCHandle(m_snapshot, m_snapshot.gcHandles[m_managedObject.gcHandlesArrayIndex])));
                    //    }
                    //}

                    if (m_object.nativeObject.isValid)
                    {
                        if (HeEditorGUI.CppButton(HeEditorGUI.SpaceR(ref position, position.height)))
                        {
                            m_owner.gotoCB(new GotoCommand(m_object.nativeObject));
                        }
                    }
                }

                switch ((Column)column)
                {
                    case Column.Type:
                        HeEditorGUI.TypeName(position, typeName);
                        //.TypeName(position, typeName, PackedManagedTypeUtility.GetInheritanceAsString(m_object.snapshot, m_object.type.packed.managedTypesArrayIndex));
                        break;

                    case Column.CppCounterpart:
                        if (m_object.nativeObject.isValid)
                            GUI.Label(position, m_object.nativeObject.name);
                        break;

                    case Column.Size:
                        HeEditorGUI.Size(position, size);
                        break;

                    case Column.Address:
                        HeEditorGUI.Address(position, m_object.address);
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
            RichManagedType m_type;
            int m_size = -1;

            public override string typeName
            {
                get
                {
                    return m_type.name;
                }
            }

            public override string assembly
            {
                get
                {
                    return m_type.assemblyName;
                }
            }

            public override string cppName
            {
                get
                {
                    return "";
                }
            }

            public override int size
            {
                get
                {
                    if (m_size == -1)
                    {
                        m_size = 0;
                        if (hasChildren)
                        {
                            for (int n = 0, nend = children.Count; n < nend; ++n)
                            {
                                var child = children[n] as AbstractItem;
                                if (child != null)
                                    m_size += child.size;
                            }
                        }
                    }
                    return m_size;
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
                m_type = new RichManagedType(snapshot, type.managedTypesArrayIndex);
            }

            public override void OnGUI(Rect position, int column)
            {
                switch ((Column)column)
                {
                    case Column.Type:
                        HeEditorGUI.TypeName(position, typeName);
                        //HeEditorGUI.TypeName(position, typeName, PackedManagedTypeUtility.GetInheritanceAsString(m_type.snapshot, m_type.packed.managedTypesArrayIndex));
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
