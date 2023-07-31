//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//

using System;
using HeapExplorer.Utilities;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;
using static HeapExplorer.Utilities.Option;

namespace HeapExplorer
{
    public class ManagedTypesControl : AbstractTreeView
    {
        public Action<Option<PackedManagedType>> onSelectionChange;

        PackedMemorySnapshot m_Snapshot;
        int m_UniqueId = 1;

        enum Column
        {
            Name,
            Size,
            ValueType,
            AssemblyName,
        }

        public ManagedTypesControl(HeapExplorerWindow window, string editorPrefsKey, TreeViewState state)
            : base(window, editorPrefsKey, state, new MultiColumnHeader(
                new MultiColumnHeaderState(new[]
                {
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("C# Type"), width = 350, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Size"), width = 80, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("ValueType", "Is this type a ValueType?"), width = 40, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Assembly"), width = 150, autoResize = true },
                })))
        {
            multiColumnHeader.canSort = true;

            Reload();
        }

        protected override void OnSelectionChanged(TreeViewItem selectedItem)
        {
            base.OnSelectionChanged(selectedItem);

            if (onSelectionChange == null)
                return;

            var item = selectedItem as ManagedTypeItem;
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

            var cycleTracker = new CycleTracker<int>();
            for (int n = 0, nend = m_Snapshot.managedTypes.Length; n < nend; ++n)
            {
                if (window.isClosing) // the window is closing
                    break;

                var type = m_Snapshot.managedTypes[n];

                var item = new ManagedTypeItem
                {
                    id = m_UniqueId++,
                    depth = root.depth + 1,
                    displayName = "",
                    itemDepth = 0,
                };
                item.Initialize(this, m_Snapshot, type.managedTypesArrayIndex);
                root.AddChild(item);

                // Add its base-classes
                var baseType = type;
                var itemDepth = 1;
                cycleTracker.markStartOfSearch();
                {while (baseType.baseOrElementTypeIndex.valueOut(out var baseOrElementTypeIndex)) {
                    if (cycleTracker.markIteration(baseOrElementTypeIndex)) {
                        cycleTracker.reportCycle(
                            $"{nameof(BuildTree)}()", baseOrElementTypeIndex, 
                            idx => m_Snapshot.managedTypes[idx].ToString()
                        );
                        break;
                    }

                    baseType = m_Snapshot.managedTypes[baseOrElementTypeIndex];

                    var baseItem = new ManagedTypeItem
                    {
                        id = m_UniqueId++,
                        depth = item.depth + 1,
                        displayName = "",
                        itemDepth = itemDepth++
                    };
                    baseItem.Initialize(this, m_Snapshot, baseType.managedTypesArrayIndex);
                    item.AddChild(baseItem);
                }}
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
            // Sort base class visualization always the same
            if (aa.parent == bb.parent && !aa.hasChildren && !bb.hasChildren)
            {
                var a = aa as AbstractItem;
                var b = bb as AbstractItem;
                return a.itemDepth.CompareTo(b.itemDepth);
            }

            var sortingColumn = multiColumnHeader.sortedColumnIndex;
            var ascending = multiColumnHeader.IsSortedAscending(sortingColumn);

            var itemA = (ascending ? aa : bb) as AbstractItem;
            var itemB = (ascending ? bb : aa) as AbstractItem;

            switch ((Column)sortingColumn)
            {
                case Column.Name:
                    return string.Compare(itemB.typeName, itemA.typeName, StringComparison.OrdinalIgnoreCase);
                case Column.ValueType:
                    return itemA.isValueType.CompareTo(itemB.isValueType);
                case Column.AssemblyName:
                    return string.Compare(itemB.assemblyName, itemA.assemblyName, StringComparison.OrdinalIgnoreCase);
            }

            return 0;
        }

        ///////////////////////////////////////////////////////////////////////////

        abstract class AbstractItem : AbstractTreeViewItem
        {
            public int itemDepth;

            public abstract string typeName
            {
                get;
            }

            public abstract string assemblyName
            {
                get;
            }

            public abstract long size
            {
                get;
            }

            public abstract bool isValueType
            {
                get;
            }
        }

        ///////////////////////////////////////////////////////////////////////////

        class ManagedTypeItem : AbstractItem
        {
            public PackedManagedType packed
            {
                get
                {
                    return m_Type.packed;
                }
            }

            public override string typeName
            {
                get
                {
                    return m_Type.name;
                }
            }

            public override long size => m_Type.packed.size.fold(v => v, v => v);

            public override string assemblyName
            {
                get
                {
                    return m_Type.assemblyName;
                }
            }

            public override bool isValueType
            {
                get
                {
                    return m_Type.packed.isValueType;
                }
            }

            RichManagedType m_Type;
            ManagedTypesControl m_Owner;

            public void Initialize(ManagedTypesControl owner, PackedMemorySnapshot snapshot, int arrayIndex)
            {
                m_Owner = owner;
                m_Type = new RichManagedType(snapshot, arrayIndex);
            }

            public override void GetItemSearchString(string[] target, out int count, out string type, out string label)
            {
                base.GetItemSearchString(target, out count, out type, out label);

                type = typeName;
                target[count++] = typeName;
            }

            public override void OnGUI(Rect position, int column)
            {
                if (column == 0)
                {
                    HeEditorGUI.ManagedTypeIcon(HeEditorGUI.SpaceL(ref position, position.height), m_Type.packed);
                }

                switch ((Column)column)
                {
                    case Column.Name:
                        HeEditorGUI.TypeName(position, typeName);
                        break;

                    case Column.Size:
                        HeEditorGUI.Size(position, size);
                        break;

                    case Column.ValueType:
                        EditorGUI.ToggleLeft(position, GUIContent.none, isValueType);
                        break;

                    case Column.AssemblyName:
                        HeEditorGUI.AssemblyName(position, assemblyName);
                        break;
                }
            }
        }
    }
}
