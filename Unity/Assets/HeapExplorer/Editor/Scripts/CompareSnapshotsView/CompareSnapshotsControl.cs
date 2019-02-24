//
// Heap Explorer for Unity. Copyright (c) 2019 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://bitbucket.org/pschraut/unityheapexplorer/
//
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace HeapExplorer
{
	public class CompareSnapshotsControl : AbstractTreeView
    {
        //public System.Action<GotoCommand> gotoCB;

        enum EColumn
        {
            Type,
            SizeA,
            SizeB,
            SizeDiff,
            CountA,
            CountB,
            CountDiff
        }

        public CompareSnapshotsControl(HeapExplorerWindow window, string editorPrefsKey, TreeViewState state)
            : base(window, editorPrefsKey, state, new MultiColumnHeader(
                new MultiColumnHeaderState(new[]
                {
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Type"), width = 300, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Size (A)"), width = 100, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Size (B)"), width = 100, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Size delta (B-A)"), width = 120, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Count (A)"), width = 100, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Count (B)"), width = 100, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Count delta (B-A)"), width = 120, autoResize = true },
                })))
        {
            multiColumnHeader.canSort = true;

            Reload();
        }

        protected override int OnSortItem(TreeViewItem aa, TreeViewItem bb)
        {
            var sortingColumn = multiColumnHeader.sortedColumnIndex;
            var ascending = multiColumnHeader.IsSortedAscending(sortingColumn);

            var itemA = (ascending ? aa : bb) as Item;
            var itemB = (ascending ? bb : aa) as Item;

            switch ((EColumn)sortingColumn)
            {
                case EColumn.Type:
                    return string.Compare(itemB.displayName, itemA.displayName, true);

                case EColumn.SizeA:
                    return itemA.size[0].CompareTo(itemB.size[0]);

                case EColumn.SizeB:
                    return itemA.size[1].CompareTo(itemB.size[1]);

                case EColumn.SizeDiff:
                    return itemA.sizeDiff.CompareTo(itemB.sizeDiff);

                case EColumn.CountA:
                    return itemA.count[0].CompareTo(itemB.count[0]);

                case EColumn.CountB:
                    return itemA.count[1].CompareTo(itemB.count[1]);

                case EColumn.CountDiff:
                    return itemA.countDiff.CompareTo(itemB.countDiff);
            }

            return 0;
        }

        public void SwapAB()
        {
            if (!rootItem.hasChildren)
                return;

            foreach(var r in rootItem.children)
                SwapR(r);
        }

        void SwapR(TreeViewItem i)
        {
            var item = i as Item;
            if (item != null)
                item.Swap();

            if (i != null && i.hasChildren)
            {
                for (int n = 0; n < i.children.Count; ++n)
                    SwapR(i.children[n]);
            }
        }

        public TreeViewItem BuildTree(PackedMemorySnapshot snapshotA, PackedMemorySnapshot snapshotB)
        {
            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
            if (snapshotA == null || snapshotB == null)
            {
                root.AddChild(new TreeViewItem { id = 1, depth = -1, displayName = "" });
                return root;
            }

            var uniqueId = 1;
            var snapshots = new[] { snapshotA, snapshotB };
            BuildNativeTree(snapshots, ref uniqueId, root);
            BuildManagedTree(snapshots, ref uniqueId, root);
            BuildMemoryTree(snapshots, ref uniqueId, root);
            BuildGCHandleTree(snapshots, ref uniqueId, root);
            
            SortItemsRecursive(root, OnSortItem);

            return root;
        }

        void BuildMemoryTree(PackedMemorySnapshot[] snapshots, ref int uniqueId, TreeViewItem root)
        {
            var parent = new Item
            {
                id = uniqueId++,
                displayName = "C# Memory Sections",
                depth = root.depth + 1,
                icon = HeEditorStyles.csImage
            };
            root.AddChild(parent);

            for (int k = 0, kend = snapshots.Length; k < kend; ++k)
            {
                var snapshot = snapshots[k];

                foreach (var section in snapshot.managedHeapSections)
                {
                    parent.size[k] += (long)section.size;
                }

                parent.count[k] += snapshot.managedHeapSections.Length;
            }
        }

        void BuildGCHandleTree(PackedMemorySnapshot[] snapshots, ref int uniqueId, TreeViewItem root)
        {
            var parent = new Item
            {
                id = uniqueId++,
                displayName = "GC Handles",
                depth = root.depth + 1,
                icon = HeEditorStyles.gcHandleImage
            };
            root.AddChild(parent);

            for (int k = 0, kend = snapshots.Length; k < kend; ++k)
            {
                var snapshot = snapshots[k];

                parent.size[k] += snapshot.gcHandles.Length * snapshot.virtualMachineInformation.pointerSize;
                parent.count[k] += snapshot.gcHandles.Length;
            }
        }

        void BuildNativeTree(PackedMemorySnapshot[] snapshots, ref int uniqueId, TreeViewItem root)
        {
            var parent = new Item
            {
                id = uniqueId++,
                displayName = "C++ Objects",
                depth = root.depth + 1,
                icon = HeEditorStyles.cppImage
            };
            root.AddChild(parent);

            var items = new List<Item>(1024);
            var table = new Dictionary<string, Item>(1024); // TypeName, Item

            for (int k = 0, kend = snapshots.Length; k < kend; ++k)
            {
                var snapshot = snapshots[k];

                for (int n = 0, nend = snapshot.nativeTypes.Length; n < nend; ++n)
                {
                    var type = snapshot.nativeTypes[n];
                    var item = default(Item);

                    if (!table.TryGetValue(type.name, out item))
                    {
                        item = new Item
                        {
                            id = uniqueId++,
                            displayName = type.name,
                            depth = parent.depth + 1,
                            icon = HeEditorStyles.cppImage
                        };

                        items.Add(item);
                        table[type.name] = item;
                    }

                    item.size[k] = type.totalObjectSize;
                    item.count[k] = type.totalObjectCount;
                }
            }

            // Remove all items that are identical between both snapshots
            for (int n = 0, nend = items.Count; n < nend; ++n)
            {
                if (items[n].countDiff != 0 && items[n].sizeDiff != 0)
                    parent.AddChild(items[n]);
            }

            Sum(parent);
        }

        void Sum(Item parent)
        {
            if (parent == null || !parent.hasChildren)
                return;

            for (int n = 0, nend = parent.children.Count; n < nend; ++n)
            {
                var item = parent.children[n] as Item;
                if (item == null)
                    continue;

                parent.size[0] += item.size[0];
                parent.size[1] += item.size[1];

                parent.count[0] += item.count[0];
                parent.count[1] += item.count[1];
            }
        }

        void BuildManagedTree(PackedMemorySnapshot[] snapshots, ref int uniqueId, TreeViewItem root)
        {
            var parent = new Item
            {
                id = uniqueId++,
                displayName = "C# Objects",
                depth = root.depth + 1,
                icon = HeEditorStyles.csImage
            };
            root.AddChild(parent);

            var items = new List<Item>(1024);
            var table = new Dictionary<string, Item>(1024); // TypeName, Item

            for (int k = 0, kend = snapshots.Length; k < kend; ++k)
            {
                var snapshot = snapshots[k];

                for (int n = 0, nend = snapshot.managedTypes.Length; n < nend; ++n)
                {
                    var type = snapshot.managedTypes[n];
                    var item = default(Item);

                    if (!table.TryGetValue(type.name, out item))
                    {
                        item = new Item
                        {
                            id = uniqueId++,
                            displayName = type.name,
                            depth = parent.depth + 1,
                            icon = HeEditorStyles.csImage
                        };

                        items.Add(item);
                        table[type.name] = item;
                    }

                    item.size[k] = type.totalObjectSize;
                    item.count[k] = type.totalObjectCount;
                }
            }

            // Remove all items that are identical between both snapshots
            for (int n = 0, nend = items.Count; n < nend; ++n)
            {
                if (items[n].countDiff != 0 && items[n].sizeDiff != 0)
                    parent.AddChild(items[n]);
            }

            Sum(parent);
        }

        class Item : AbstractTreeViewItem
        {
            public long[] size = new long[2];
            public long[] count = new long[2];

            public long sizeDiff
            {
                get
                {
                    return size[1] - size[0];
                }
            }

            public long countDiff
            {
                get
                {
                    return count[1] - count[0];
                }
            }

            const string k_UnknownTypeString = "<unknown type>";

            public void Swap()
            {
                var s = size[0];
                size[0] = size[1];
                size[1] = s;
            }

            public override void GetItemSearchString(string[] target, out int count)
            {
                count = 0;
                target[count++] = displayName ?? k_UnknownTypeString;
            }

            public override void OnGUI(Rect position, int column)
            {
                if (column == 0 && this.icon != null)
                {
                    GUI.Box(HeEditorGUI.SpaceL(ref position, position.height), this.icon, HeEditorStyles.iconStyle);
                }

                switch((EColumn)column)
                {
                    case EColumn.Type:
                        HeEditorGUI.TypeName(position, displayName ?? k_UnknownTypeString);
                        break;

                    case EColumn.SizeA:
                        HeEditorGUI.Size(position, size[0]);
                        break;

                    case EColumn.SizeB:
                        HeEditorGUI.Size(position, size[1]);
                        break;

                    case EColumn.SizeDiff:
                        HeEditorGUI.SizeDiff(position, sizeDiff);
                        break;

                    case EColumn.CountA:
                        HeEditorGUI.Count(position, count[0]);
                        break;

                    case EColumn.CountB:
                        HeEditorGUI.Count(position, count[1]);
                        break;

                    case EColumn.CountDiff:
                        HeEditorGUI.CountDiff(position, countDiff);
                        break;
                }
            }
        }
    }
}
