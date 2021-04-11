﻿//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;

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
        IList<int> m_Expanded = new List<int>(32);
        TreeViewItem m_Tree;
        TreeViewItem m_ActiveTree;
        string[] m_SearchCache = new string[32];
        System.Text.StringBuilder m_SearchBuilder = new System.Text.StringBuilder();
        bool m_HasSearch;

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
            m_ActiveTree = m_Tree;
            Reload();
        }

        protected override bool CanMultiSelect(TreeViewItem item)
        {
            return false;
        }

        protected override TreeViewItem BuildRoot()
        {
            if (m_ActiveTree != null)
                return m_ActiveTree;

            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
            root.AddChild(new TreeViewItem { id = root.id + 1, depth = -1, displayName = "" });
            return root;
        }

        void OnSortingChanged(MultiColumnHeader multiColumnHeader)
        {
            if (rootItem == null || !rootItem.hasChildren)
                return;

            SortItemsRecursive(m_ActiveTree, OnSortItem);
            Reload();
        }

        protected abstract int OnSortItem(TreeViewItem x, TreeViewItem y);

        protected void SortItemsRecursive(TreeViewItem parent, System.Comparison<TreeViewItem> comparison)
        {
            if (parent == null)
                return;

            var sortMe = new List<TreeViewItem>();
            sortMe.Add(parent);

            for (var n = 0; n < sortMe.Count; ++n)
            {
                var item = sortMe[n];
                if (item.hasChildren)
                {
                    item.children.Sort(comparison);
                    sortMe.AddRange(item.children); // sort items of children too (kind of recursive)
                }
            }
        }

        public void Search(string search)
        {
            m_HasSearch = !string.IsNullOrEmpty(search);

            var selectedId = -1;
            var selection = new List<int>(this.GetSelection());
            if (selection != null && selection.Count != 0)
                selectedId = selection[0];
            selection.Clear();

            m_Search = SearchTextParser.Parse(search);

            // I don't use the TreeViewControl base.searchString API, because I didn't get
            // it to work to display filtered content sorted.
            // Therefore I perform the filter myself as seen below, by building a new tree
            // containing a flat list of filtered items only.
            //base.searchString = search;

            if (!m_HasSearch)
            {
                m_ActiveTree = m_Tree;
            }
            else
            {
                // Create a node that contains all items that match the search
                m_ActiveTree = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };

                // Get all items in the tree as a flat list
                var itemsToProcess = new List<TreeViewItem>();
                itemsToProcess.Add(m_Tree);
                for (var n = 0; n < itemsToProcess.Count; ++n)
                {
                    var item = itemsToProcess[n];
                    if (item == null)
                        continue;

                    if (item.hasChildren)
                        itemsToProcess.AddRange(item.children); // process all children too (this makes it kind of recursive)

                    if (item.id == 0)
                        continue; // ignore tree root node itself

                    if (!DoesItemMatchSearch(item, search))
                        continue;

                    // Keep track of the selected item
                    if (selectedId == item.id)
                    {
                        selection = new List<int>();
                        selection.Add(selectedId);
                    }

                    m_ActiveTree.AddChild(item);
                }

                // If the search didn't find any item, make sure to add at least one
                // invisible item, otherwise the TreeViewControl displays an error.
                if (!m_ActiveTree.hasChildren)
                    m_ActiveTree.AddChild(new TreeViewItem { id = m_ActiveTree.id + 1, depth = -1, displayName = "" });
            }

            SortItemsRecursive(m_ActiveTree, OnSortItem);
            Reload();

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
                int searchCount;
                string type;
                string label;
                i.GetItemSearchString(m_SearchCache, out searchCount, out type, out label);

                if (!m_Search.IsTypeMatch(type) || !m_Search.IsLabelMatch(label))
                    return false;

                m_SearchBuilder.Length = 0;
                for (var n=0; n < searchCount; ++n)
                {
                    m_SearchBuilder.Append(m_SearchCache[n]);
                    m_SearchBuilder.Append(" ");
                }
                m_SearchBuilder.Append("\0");

                return m_Search.IsNameMatch(m_SearchBuilder.ToString());
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
                    if (m_HasSearch)
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

        public abstract void OnGUI(Rect position, int column);
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
