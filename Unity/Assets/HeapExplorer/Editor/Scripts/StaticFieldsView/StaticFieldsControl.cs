using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;

namespace HeapExplorer
{
    public class StaticFieldsControl : AbstractTreeView
    {
        public System.Action<GotoCommand> gotoCB;
        public System.Action<PackedManagedType?> onTypeSelected;

        public int count
        {
            get
            {
                if (rootItem == null || !rootItem.hasChildren)
                    return 0;

                return rootItem.children.Count;
            }
        }

        PackedMemorySnapshot m_snapshot;
        int m_uniqueId = 1;

        enum Column
        {
            Type,
            Size,
            Refs,
            Assembly,
        }

        public StaticFieldsControl(string editorPrefsKey, TreeViewState state)
            : base(editorPrefsKey, state, new MultiColumnHeader(
                new MultiColumnHeaderState(new[]
                {
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Type"), width = 250, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Size"), width = 80, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Refs", "The number of C# objects the static fields of this type reference."), width = 80, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Assembly"), width = 150, autoResize = true },
                })))
        {
            multiColumnHeader.canSort = true;
            //multiColumnHeader.sortingChanged += OnSortingChanged;

            Reload();
        }

        public void Select(PackedManagedType managedType)
        {
            var item = FindItemByType(rootItem, managedType.managedTypesArrayIndex);
            SelectItem(item);
        }

        TreeViewItem FindItemByType(TreeViewItem parent, int typeIndex)
        {
            if (parent != null)
            {
                var item = parent as StaticTypeItem;
                if (item != null && item.type.managedTypesArrayIndex == typeIndex)
                    return item;

                if (parent.hasChildren)
                {
                    for (int n = 0, nend = parent.children.Count; n < nend; ++n)
                    {
                        var child = parent.children[n];

                        var value = FindItemByType(child, typeIndex);
                        if (value != null)
                            return value;
                    }
                }
            }

            return null;
        }

        //void OnSortingChanged(MultiColumnHeader multiColumnHeader)
        //{
        //    //Reload();
        //    SetTree(BuildTree(m_snapshot));
        //}

        protected override void OnSelectionChanged(TreeViewItem selectedItem)
        {
            base.OnSelectionChanged(selectedItem);

            if (selectedItem != null && selectedItem is StaticTypeItem)
            {
                var item = selectedItem as StaticTypeItem;
                if (onTypeSelected != null)
                    onTypeSelected.Invoke(item.type);
                return;
            }
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
            
            for (int n = 0, nend = m_snapshot.managedStaticTypes.Length; n < nend; ++n)
            {
                var type = m_snapshot.managedTypes[m_snapshot.managedStaticTypes[n]];

                var group = new StaticTypeItem
                {
                    id = m_uniqueId++,
                    depth = 0,
                    displayName = ""
                };
                group.Initialize(this, m_snapshot, type);
                
                root.AddChild(group);
            }

            SortItemsRecursive(root, OnSortItem);

            return root;
        }


        protected override int OnSortItem(TreeViewItem aa, TreeViewItem bb)
        {
            var sortingColumn = multiColumnHeader.sortedColumnIndex;
            var ascending = multiColumnHeader.IsSortedAscending(sortingColumn);

            var itemA = (ascending ? aa : bb) as StaticTypeItem;
            var itemB = (ascending ? bb : aa) as StaticTypeItem;

            return itemA.Compare((Column)sortingColumn, itemB);
        }
        
        ///////////////////////////////////////////////////////////////////////////

        class StaticTypeItem : AbstractTreeViewItem
        {
            StaticFieldsControl m_owner;
            PackedManagedType m_typeDescription;

            public PackedManagedType type
            {
                get
                {
                    return m_typeDescription;
                }
            }

            public string assemblyName
            {
                get
                {
                    return m_typeDescription.assembly;
                }
            }

            public string typeName
            {
                get
                {
                    return m_typeDescription.name;
                }
            }

            public int size
            {
                get
                {
                    if (m_typeDescription.staticFieldBytes != null)
                        return m_typeDescription.staticFieldBytes.Length;

                    return 0;
                }
            }

            int m_referencesCount = -1;
            public int referencesCount
            {
                get
                {
                    if (m_referencesCount == -1)
                    {
                        m_referencesCount = 0;

                        var list = new List<int>();
                        m_owner.m_snapshot.FindManagedStaticFieldsOfType(m_typeDescription, list);

                        for (int n=0, nend = list.Count; n < nend; ++n)
                        {
                            int refCount, refByCount;
                            m_owner.m_snapshot.GetConnectionsCount(PackedConnection.Kind.StaticField, list[n], out refCount, out refByCount);

                            m_referencesCount += refCount;
                        }
                    }

                    return m_referencesCount;
                }
            }

            public void Initialize(StaticFieldsControl owner, PackedMemorySnapshot snapshot, PackedManagedType typeDescription)
            {
                m_owner = owner;
                m_typeDescription = typeDescription;
            }

            public override void GetItemSearchString(string[] target, out int count)
            {
                count = 0;
                target[count++] = typeName;
                target[count++] = assemblyName;
            }

            public override void OnGUI(Rect position, int column)
            {
                if (column == 0)
                {
                    GUI.Box(HeEditorGUI.SpaceL(ref position, position.height), HeEditorStyles.csStaticImage, HeEditorStyles.iconStyle);
                }

                switch ((Column)column)
                {
                    case Column.Type:
                        HeEditorGUI.TypeName(position, typeName);
                        break;

                    case Column.Size:
                        HeEditorGUI.Size(position, size);
                        break;

                    case Column.Refs:
                        GUI.Label(position, referencesCount.ToString());
                        break;

                    case Column.Assembly:
                        GUI.Label(position, assemblyName);
                        break;
                }
            }

            public int Compare(Column column, TreeViewItem other)
            {
                var otherItem = other as StaticTypeItem;
                if (otherItem == null)
                    return 1;

                switch (column)
                {
                    case Column.Type:
                        return string.Compare(otherItem.typeName, this.typeName, true);

                    case Column.Size:
                        return this.size.CompareTo(otherItem.size);

                    case Column.Refs:
                        return this.referencesCount.CompareTo(otherItem.referencesCount);

                    case Column.Assembly:
                        return string.Compare(otherItem.assemblyName, this.assemblyName, true);
                }

                return 0;
            }
        }
    }
}
