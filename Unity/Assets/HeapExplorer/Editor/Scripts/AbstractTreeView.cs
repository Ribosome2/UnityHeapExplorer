﻿using System.Collections;
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

        public System.Action findPressed;

        SearchTextParser.Result m_search = new SearchTextParser.Result();
        protected string m_editorPrefsKey;
        int m_firstVisibleRow;
        IList<int> m_expanded = new List<int>(32);
        TreeViewItem m_tree;
        public HeapExplorerWindow m_Window;

        public AbstractTreeView(HeapExplorerWindow window, string editorPrefsKey, TreeViewState state)
            : base(state)
        {
            m_Window = window;
            m_editorPrefsKey = editorPrefsKey;

            rowHeight = 20;
            showAlternatingRowBackgrounds = true;
            showBorder = false;
            columnIndexForTreeFoldouts = 0;

            LoadLayout();
        }

        public AbstractTreeView(HeapExplorerWindow window, string editorPrefsKey, TreeViewState state, MultiColumnHeader multiColumnHeader)
            : base(state, multiColumnHeader)
        {
            m_Window = window;
            m_editorPrefsKey = editorPrefsKey;

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
            m_tree = tree;

            Reload();
        }

        protected override bool CanMultiSelect(TreeViewItem item)
        {
            return false;
        }

        protected override TreeViewItem BuildRoot()
        {
            if (m_tree != null)
                return m_tree;

            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
            root.AddChild(new TreeViewItem { id = root.id + 1, depth = -1, displayName = "" });
            return root;
        }

        void OnSortingChanged(MultiColumnHeader multiColumnHeader)
        {
            if (rootItem == null || !rootItem.hasChildren)
                return;

            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
            foreach (var item in rootItem.children)
                root.AddChild(item);

            SortItemsRecursive(root, OnSortItem);
            SetTree(root);
        }

        protected abstract int OnSortItem(TreeViewItem x, TreeViewItem y);

        protected void SortItemsRecursive(TreeViewItem parent, System.Comparison<TreeViewItem> comparison)
        {
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
            var selection = this.GetSelection();

            m_search = SearchTextParser.Parse(search);
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

            for (var n= m_expanded.Count-1; n>=0; --n)
            {
                var id = m_expanded[n];
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

            m_expanded = GetExpanded();

            for (var n = m_expanded.Count - 1; n >= 0; --n)
            {
                var id = m_expanded[n];

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

            m_search = new SearchTextParser.Result();
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

        string[] m_searchCache = new string[32];
        System.Text.StringBuilder m_searchBuilder = new System.Text.StringBuilder();

        protected override bool DoesItemMatchSearch(TreeViewItem item, string search)
        {
            var i = item as AbstractTreeViewItem;
            if (i != null)
            {
                int searchCount;
                i.GetItemSearchString(m_searchCache, out searchCount);

                m_searchBuilder.Length = 0;
                for (var n=0; n < searchCount; ++n)
                {
                    m_searchBuilder.Append(m_searchCache[n]);
                    m_searchBuilder.Append(" ");
                }
                m_searchBuilder.Append("\0");

                if (m_search.IsNameMatch(m_searchBuilder.ToString()))
                    return true;

                return false;

               //return i.DoesItemMatchSearch(m_search);
            }

            return base.DoesItemMatchSearch(item, search);
        }

        protected override void BeforeRowsGUI()
        {
            UnityEngine.Profiling.Profiler.BeginSample(GetType().Name + ".BeforeRowsGUI");
            base.BeforeRowsGUI();

            int lastVisibleRow;
            GetFirstAndLastVisibleRows(out m_firstVisibleRow, out lastVisibleRow);
            UnityEngine.Profiling.Profiler.EndSample();
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            UnityEngine.Profiling.Profiler.BeginSample(GetType().Name + ".RowGUI");

            var item = args.item as AbstractTreeViewItem;
            //if (item == null)
            //    return;

            if (item != null && !item.enabled)
                EditorGUI.BeginDisabledGroup(true);

            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
            {
                var rect = args.GetCellRect(i);

                if (args.row == m_firstVisibleRow)
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
                    rect = TreeViewUtility.IndentByDepth(args.item, rect);
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
            EditorPrefs.SetString(m_editorPrefsKey, json);
        }

        public void LoadLayout()
        {
            var json = EditorPrefs.GetString(m_editorPrefsKey, "");
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

        public virtual void GetItemSearchString(string[] target, out int count)
        {
            count = 0;
        }

        //public abstract bool DoesItemMatchSearch(SearchTextParser.Result search);
        public abstract void OnGUI(Rect position, int column);
    }
}
