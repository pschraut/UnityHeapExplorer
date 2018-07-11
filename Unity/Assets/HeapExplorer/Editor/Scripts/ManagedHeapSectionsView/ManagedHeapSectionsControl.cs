//#define HEAPEXPLORER_DISPLAY_REFS
using UnityEngine;
using UnityEditor.IMGUI.Controls;

namespace HeapExplorer
{
    public class ManagedHeapSectionsControl : AbstractTreeView
    {
        //public System.Action<GotoCommand> gotoCB;
        public System.Action<PackedMemorySection?> onSelectionChange;

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
            Address,
            Size,
#if HEAPEXPLORER_DISPLAY_REFS
            Refs,
#endif
        }

        public ManagedHeapSectionsControl(HeapExplorerWindow window, string editorPrefsKey, TreeViewState state)
            : base(window, editorPrefsKey, state, new MultiColumnHeader(
                new MultiColumnHeaderState(new[]
                {
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Address"), width = 300, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Size"), width = 120, autoResize = true },
#if HEAPEXPLORER_DISPLAY_REFS
                    new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Refs"), width = 120, autoResize = true },
#endif
                })))
        {
            extraSpaceBeforeIconAndLabel = 4;
            columnIndexForTreeFoldouts = 0;
            multiColumnHeader.canSort = true;

            Reload();
        }
        
        public void Clear()
        {
            m_snapshot = null;

            Reload();
        }

        protected override void OnSelectionChanged(TreeViewItem selectedItem)
        {
            base.OnSelectionChanged(selectedItem);

            if (onSelectionChange == null)
                return;

            var item = selectedItem as HeapSectionItem;
            if (item == null)
            {
                onSelectionChange.Invoke(null);
                return;
            }

            var section = m_snapshot.managedHeapSections[item.m_arrayIndex];
            onSelectionChange.Invoke(section);
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
            
            for (int n = 0, nend = m_snapshot.managedHeapSections.Length; n < nend; ++n)
            {
                var item = new HeapSectionItem()
                {
                    id = m_uniqueId++,
                    depth = root.depth + 1,
                };

                item.Initialize(this, m_snapshot, n);
                root.AddChild(item);
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
                case Column.Size:
                    return itemA.m_size.CompareTo(itemB.m_size);

                case Column.Address:
                    return itemA.m_address.CompareTo(itemB.m_address);

#if HEAPEXPLORER_DISPLAY_REFS
                case Column.Refs:
                    return itemA.m_refs.CompareTo(itemB.m_refs);
#endif
            }

            return 0;
        }

        ///////////////////////////////////////////////////////////////////////////
        // TreeViewItem's
        ///////////////////////////////////////////////////////////////////////////

        class AbstractItem : AbstractTreeViewItem
        {
            protected ManagedHeapSectionsControl m_owner;
            public System.UInt64 m_address;
            public ulong m_size;
#if HEAPEXPLORER_DISPLAY_REFS
            public int m_refs;
#endif

            public override void GetItemSearchString(string[] target, out int count)
            {
                count = 0;
                target[count++] = displayName;
                target[count++] = string.Format(StringFormat.Address, m_address);
            }

            public override void OnGUI(Rect position, int column)
            {
                switch ((Column)column)
                {
                    case Column.Address:
                        HeEditorGUI.Address(position, m_address);
                        break;

                    case Column.Size:
                        HeEditorGUI.Size(position, (long)m_size);
                        break;

#if HEAPEXPLORER_DISPLAY_REFS
                    case Column.Refs:
                        GUI.Label(position, m_refs.ToString());
                        break;
#endif
                }
            }
        }

        // ------------------------------------------------------------------------

        class HeapSectionItem : AbstractItem
        {
            PackedMemorySnapshot m_snapshot;
            public int m_arrayIndex;

            public void Initialize(ManagedHeapSectionsControl owner, PackedMemorySnapshot snapshot, int memorySegmentIndex)
            {
                m_owner = owner;
                m_snapshot = snapshot;
                m_arrayIndex = memorySegmentIndex;

                displayName = "MemorySection";
                m_address = m_snapshot.managedHeapSections[m_arrayIndex].startAddress;
                if (m_snapshot.managedHeapSections[m_arrayIndex].bytes != null)
                {
                    m_size = (ulong)m_snapshot.managedHeapSections[m_arrayIndex].bytes.LongLength;

#if HEAPEXPLORER_DISPLAY_REFS
                    m_snapshot.GetConnectionsCount(m_snapshot.managedHeapSections[m_arrayIndex], out m_refs);
#endif
                }
            }

            public override void OnGUI(Rect position, int column)
            {
                if (column == 0)
                {
                    if (HeEditorGUI.CsButton(HeEditorGUI.SpaceL(ref position, position.height)))
                    {
                        MemoryWindow.Inspect(m_snapshot, m_address, m_size);
                    }
                }

                base.OnGUI(position, column);
            }
        }

        //class HeapGapItem : AbstractItem
        //{
        //    PackedMemorySnapshot m_snapshot;
        //    public int m_arrayIndex;
        //
        //    public void Initialize(ManagedHeapSectionsControl owner, PackedMemorySnapshot snapshot, ulong address, ulong size)
        //    {
        //        m_owner = owner;
        //        m_snapshot = snapshot;
        //
        //        displayName = "Waste";
        //        m_address = address;
        //        m_size = size;
        //    }
        //
        //    public override void OnGUI(Rect position, int column)
        //    {
        //        var oldcolor = GUI.color;
        //        GUI.color = new Color(1, 0, 0, 0.25f);
        //        GUI.DrawTexture(position, EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill);
        //        GUI.color = oldcolor;
        //
        //        base.OnGUI(position, column);
        //    }
        //}
    }
}
