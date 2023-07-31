﻿//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//

using System;
using System.Collections.Generic;
using HeapExplorer.Utilities;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;
using static HeapExplorer.Utilities.Option;

namespace HeapExplorer
{
    abstract public class AbstractTreeView : UnityEditor.IMGUI.Controls.TreeView
    {
        public new string searchString
        {
            get
            {
                return base.searchString;
            }
        }

        public HeapExplorerWindow window
        {
            get;
            private set;
        }

        public System.Action findPressed;

        SearchTextParser.Result m_Search = new SearchTextParser.Result();
        protected string m_EditorPrefsKey;
        int m_FirstVisibleRow;
        private List<TreeViewItem> m_RowsCache;
        IList<int> m_Expanded = new List<int>(32);
        TreeViewItem m_Tree;
        string[] m_SearchCache = new string[32];

        public AbstractTreeView(HeapExplorerWindow window, string editorPrefsKey, TreeViewState state)
            : base(state)
        {
            this.window = window;
            m_EditorPrefsKey = editorPrefsKey;

            rowHeight = 20;
            showAlternatingRowBackgrounds = true;
            showBorder = false;
            columnIndexForTreeFoldouts = 0;

            LoadLayout();
        }

        public AbstractTreeView(HeapExplorerWindow window, string editorPrefsKey, TreeViewState state, MultiColumnHeader multiColumnHeader)
            : base(state, multiColumnHeader)
        {
            this.window = window;
            m_EditorPrefsKey = editorPrefsKey;

            rowHeight = 20;
            showAlternatingRowBackgrounds = true;
            showBorder = false;
            columnIndexForTreeFoldouts = 0;
            extraSpaceBeforeIconAndLabel = 0;
            baseIndent = 0;

            multiColumnHeader.sortingChanged += OnSortingChanged;

            LoadLayout();
        }

        public void SetTree(TreeViewItem tree)
        {
            m_Tree = tree;
            Reload();
        }

        protected override bool CanMultiSelect(TreeViewItem item)
        {
            return false;
        }

        protected override TreeViewItem BuildRoot()
        {
            if (m_Tree != null)
                return m_Tree;

            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
            root.AddChild(new TreeViewItem { id = root.id + 1, depth = -1, displayName = "" });
            return root;
        }

        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
        {
            if (m_RowsCache == null)
                m_RowsCache = new List<TreeViewItem>(128);
            m_RowsCache.Clear();

            if (hasSearch)
            {
                SearchTree(root, searchString, m_RowsCache);
                m_RowsCache.Sort(CompareItem);
            }
            else
            {
                SortAndAddExpandedRows(root, m_RowsCache);
            }

            return m_RowsCache;
        }

        protected virtual void SearchTree(TreeViewItem root, string search, List<TreeViewItem> result)
        {
            var stack = new Stack<TreeViewItem>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                TreeViewItem current = stack.Pop();
                if (current.children != null)
                {
                    foreach (var child in current.children)
                    {
                        if (child != null)
                        {
                            if (DoesItemMatchSearch(child, search))
                                result.Add(child);

                            stack.Push(child);
                        }
                    }
                }
            }
        }

        protected virtual void SortAndAddExpandedRows(TreeViewItem root, IList<TreeViewItem> rows)
        {
            if (root.hasChildren)
            {
                root.children.Sort(CompareItem);
                foreach (TreeViewItem child in root.children)
                {
                    GetAndSortExpandedRowsRecursive(child, rows);
                }
            }
        }

        void GetAndSortExpandedRowsRecursive(TreeViewItem item, IList<TreeViewItem> expandedRows)
        {
            if (item == null)
                Debug.LogError("Found a TreeViewItem that is null. Invalid use of AddExpandedRows(): This method is only valid to call if you have built the full tree of TreeViewItems.");

            expandedRows.Add(item);

            if (item.hasChildren && IsExpanded(item.id))
            {
                item.children.Sort(CompareItem);
                foreach (TreeViewItem child in item.children)
                {
                    GetAndSortExpandedRowsRecursive(child, expandedRows);
                }
            }
        }

        void OnSortingChanged(MultiColumnHeader multiColumnHeader)
        {
            if (rootItem == null || !rootItem.hasChildren)
                return;

            Reload();
        }

        protected abstract int OnSortItem(TreeViewItem x, TreeViewItem y);

        protected int CompareItem(TreeViewItem x, TreeViewItem y)
        {
            int result = OnSortItem(x, y);
            if (result == 0)
                return x.id.CompareTo(y.id);
            return result;
        }

        public void Search(string search)
        {
            var selection = new List<int>(this.GetSelection());

            m_Search = SearchTextParser.Parse(search);
            base.searchString = search;

            if (selection != null && selection.Count > 0)
                this.SetSelection(selection, TreeViewSelectionOptions.FireSelectionChanged | TreeViewSelectionOptions.RevealAndFrame);
        }

        public virtual void OnGUI()
        {
            UnityEngine.Profiling.Profiler.BeginSample(GetType().Name + ".OnGUI");
            OnGUI(GUILayoutUtility.GetRect(50, 100000, 50, 100000));
            UnityEngine.Profiling.Profiler.EndSample();

            if (HasFocus())
            {
                CommandEventHandlingInternal();
            }
        }

        void CommandEventHandlingInternal()
        {
            var current = Event.current;

            if (current.commandName == "Find")
            {
                if (current.type == EventType.ExecuteCommand)
                {
                    if (findPressed != null)
                        findPressed();
                }

                if (current.type == EventType.ExecuteCommand || current.type == EventType.ValidateCommand)
                    current.Use();
            }
        }

        protected override void ExpandedStateChanged()
        {
            UnityEngine.Profiling.Profiler.BeginSample(GetType().Name + ".ExpandedStateChanged");

            base.ExpandedStateChanged();

            for (var n= m_Expanded.Count-1; n>=0; --n)
            {
                var id = m_Expanded[n];
                if (!IsExpanded(id))
                {
                    var item = FindItem(id, rootItem) as AbstractTreeViewItem;
                    if (item != null)
                    {
                        item.isExpanded = false;
                        OnExpandedChanged(item, false);
                    }
                }
            }

            m_Expanded = GetExpanded();

            for (var n = m_Expanded.Count - 1; n >= 0; --n)
            {
                var id = m_Expanded[n];

                var item = FindItem(id, rootItem) as AbstractTreeViewItem;
                if (item != null && !item.isExpanded)
                {
                    item.isExpanded = true;
                    OnExpandedChanged(item, true);
                }
            }
            UnityEngine.Profiling.Profiler.EndSample();
        }

        protected virtual void OnExpandedChanged(TreeViewItem item, bool expanded)
        {

        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            base.SelectionChanged(selectedIds);

            TreeViewItem selectedItem = null;

            if (selectedIds != null && selectedIds.Count > 0)
                selectedItem = FindItem(selectedIds[0], rootItem);

            OnSelectionChanged(selectedItem);
        }

        protected virtual void OnSelectionChanged(TreeViewItem selectedItem)
        {
        }

        protected void SelectItem(TreeViewItem item)
        {
            if (item == null)
                return;

            m_Search = new SearchTextParser.Result();
            base.searchString = "";

            // If the same item is selected already, nothing to do
            var currentSelection = GetSelection();
            if (currentSelection != null && currentSelection.Count > 0 && currentSelection[0] == item.id)
            {
                this.FrameItem(item.id);
                return;
            }

            SetSelection(new[] { item.id }, TreeViewSelectionOptions.RevealAndFrame | TreeViewSelectionOptions.FireSelectionChanged);
        }

        protected override bool DoesItemMatchSearch(TreeViewItem item, string search)
        {
            var i = item as AbstractTreeViewItem;
            if (i != null)
            {
                if (!i.m_MaybeCachedItemSearchString.valueOut(out var searchString)) {
                    int searchCount;
                    string type;
                    string label;
                    i.GetItemSearchString(m_SearchCache, out searchCount, out type, out label);

                    var names = new List<string>(capacity: searchCount);
                    for (var n=0; n < searchCount; ++n) 
                    {
                        var str = m_SearchCache[n];
                        if (!string.IsNullOrEmpty(str)) names.Add(str.ToLowerInvariant());
                    }

                    searchString = new AbstractTreeViewItem.Cache(
                        lowerCasedNames: names.ToArray(), type: type, label: label
                    );
                    i.m_MaybeCachedItemSearchString = Some(searchString);
                }

                if (!m_Search.IsTypeMatch(searchString.type) || !m_Search.IsLabelMatch(searchString.label)) {
                    return false;
                }
                else {
                    // ReSharper disable once LoopCanBeConvertedToQuery
                    foreach (var lowerCasedName in searchString.lowerCasedNames) {
                        if (m_Search.IsNameMatch(lowerCasedName)) return true;
                    }

                    return false;
                }
            }

            return base.DoesItemMatchSearch(item, search);
        }

        protected override void BeforeRowsGUI()
        {
            UnityEngine.Profiling.Profiler.BeginSample(GetType().Name + ".BeforeRowsGUI");
            base.BeforeRowsGUI();

            int lastVisibleRow;
            GetFirstAndLastVisibleRows(out m_FirstVisibleRow, out lastVisibleRow);
            UnityEngine.Profiling.Profiler.EndSample();
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            UnityEngine.Profiling.Profiler.BeginSample(GetType().Name + ".RowGUI");

            var item = args.item as AbstractTreeViewItem;
            if (item != null && !item.enabled)
                EditorGUI.BeginDisabledGroup(true);

            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
            {
                var rect = args.GetCellRect(i);

                if (args.row == m_FirstVisibleRow)
                {
                    var r = rect;
                    r.x += r.width + (i > 0 ? 2 : -1);
                    r.width = 1;
                    r.height = 10000;
                    var oldColor = GUI.color;
                    GUI.color = new Color(0, 0, 0, 0.15f);
                    GUI.DrawTexture(r, EditorGUIUtility.whiteTexture);
                    GUI.color = oldColor;
                }

                if (i == 0)
                {
                    rect.x += extraSpaceBeforeIconAndLabel;
                    rect.width -= extraSpaceBeforeIconAndLabel;

                    // Display the tree as a flat list when content is filtered
                    if (hasSearch)
                        rect = TreeViewUtility.IndentByDepth(0, rect);
                    else
                        rect = TreeViewUtility.IndentByDepth(args.item.depth, rect);
                }

                if (item != null)
                {
                    var column = args.GetColumn(i);
                    item.OnGUI(rect, column);
                }
            }

            if (item != null && !item.enabled)
                EditorGUI.EndDisabledGroup();

            UnityEngine.Profiling.Profiler.EndSample();
        }

        [System.Serializable]
        class SaveTreeViewState
        {
            public int[] visibleColumns;
            public int[] sortedColumns;
            public float[] columnsWidths;
            public int sortedColumnIndex;
        }

        public void SaveLayout()
        {
            var save = new SaveTreeViewState();
            save.visibleColumns = multiColumnHeader.state.visibleColumns;
            save.sortedColumns = multiColumnHeader.state.sortedColumns;
            save.sortedColumnIndex = multiColumnHeader.state.sortedColumnIndex;

            var widths = new List<float>();
            foreach (var column in multiColumnHeader.state.columns)
                widths.Add(column.width);
            save.columnsWidths = widths.ToArray();

            var json = JsonUtility.ToJson(save, true);
            //Debug.Log("Save\n" + json);
            EditorPrefs.SetString(m_EditorPrefsKey, json);
        }

        public void LoadLayout()
        {
            var json = EditorPrefs.GetString(m_EditorPrefsKey, "");
            if (string.IsNullOrEmpty(json))
            {
                if (multiColumnHeader.canSort)
                {
                    multiColumnHeader.sortedColumnIndex = 0;
                }
                return;
            }

            try
            {
                var data = JsonUtility.FromJson<SaveTreeViewState>(json);
                var columns = multiColumnHeader.state.columns;

                if (columns.Length >= data.visibleColumns.Length && data.visibleColumns.Length > 0)
                    multiColumnHeader.state.visibleColumns = data.visibleColumns;

                if (columns.Length >= data.sortedColumns.Length && data.sortedColumns.Length > 0)
                    multiColumnHeader.state.sortedColumns = data.sortedColumns;

                if (columns.Length > data.sortedColumnIndex && data.sortedColumnIndex >= 0)
                    multiColumnHeader.sortedColumnIndex = data.sortedColumnIndex;
                else
                    multiColumnHeader.sortedColumnIndex = 0;

                for (var n = 0; n < Mathf.Min(data.columnsWidths.Length, columns.Length); ++n)
                {
                    if (n >= columns.Length)
                        break;

                    columns[n].width = data.columnsWidths[n];
                }
            }
            catch { }
        }
    }


    public abstract class AbstractTreeViewItem : TreeViewItem
    {
        public bool enabled = true;
        public bool isExpanded;

        public virtual void GetItemSearchString(string[] target, out int count, out string type, out string label)
        {
            count = 0;
            type = null;
            label = null;
        }

        /// <summary>
        /// Results of <see cref="GetItemSearchString"/> are cached here to avoid re-computation. If this is `None`,
        /// invoke the <see cref="GetItemSearchString"/> and store the result here.
        /// </summary>
        public Option<Cache> m_MaybeCachedItemSearchString;

        public abstract void OnGUI(Rect position, int column);

        public sealed class Cache {
            /// <summary>
            /// Parameters for <see cref="SearchTextParser.Result.IsNameMatch"/>.
            /// <para/>
            /// The search will match if any of these matches.
            /// </summary>
            public readonly string[] lowerCasedNames;
            
            /// <summary>Parameter for <see cref="SearchTextParser.Result.IsTypeMatch"/>.</summary>
            public readonly string type;
            
            /// <summary>Parameter for <see cref="SearchTextParser.Result.IsLabelMatch"/>.</summary>
            public readonly string label;

            public Cache(string[] lowerCasedNames, string type, string label) {
                this.lowerCasedNames = lowerCasedNames;
                this.type = type;
                this.label = label;
            } 
        } 
    }

    public static class TreeViewUtility
    {
        public static Rect IndentByDepth(int itemDepth, Rect rect)
        {
            var foldoutWidth = 14;
            var indent = itemDepth + 1;

            rect.x += indent * foldoutWidth;
            rect.width -= indent * foldoutWidth;

            return rect;
        }
    }
}
