﻿//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//

using HeapExplorer.Utilities;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using static HeapExplorer.Utilities.Option;

namespace HeapExplorer
{
    // A large number of objects with the same value is inefficient from the point of memory usage.
    public class ManagedObjectDuplicatesView : HeapExplorerView
    {
        ManagedObjectDuplicatesControl m_ObjectsControl;
        HeSearchField m_ObjectsSearchField;
        ConnectionsView m_ConnectionsView;
        Option<RichManagedObject> m_Selected;
        RootPathView m_RootPathView;
        PropertyGridView m_PropertyGridView;
        float m_SplitterHorzPropertyGrid = 0.32f;
        float m_SplitterVertConnections = 0.3333f;
        float m_SplitterVertRootPath = 0.3333f;

        [InitializeOnLoadMethod]
        static void Register()
        {
            HeapExplorerWindow.Register<ManagedObjectDuplicatesView>();
        }

        public override void Awake()
        {
            base.Awake();

            titleContent = new GUIContent("C# Object Duplicates", "");
            viewMenuOrder = 255;
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            m_ConnectionsView = CreateView<ConnectionsView>();
            m_ConnectionsView.editorPrefsKey = GetPrefsKey(() => m_ConnectionsView);

            m_RootPathView = CreateView<RootPathView>();
            m_RootPathView.editorPrefsKey = GetPrefsKey(() => m_RootPathView);

            m_PropertyGridView = CreateView<PropertyGridView>();
            m_PropertyGridView.editorPrefsKey = GetPrefsKey(() => m_PropertyGridView);

            m_ObjectsControl = new ManagedObjectDuplicatesControl(window, GetPrefsKey(() => m_ObjectsControl), new TreeViewState());
            m_ObjectsControl.onSelectionChange += OnListViewSelectionChange;

            m_ObjectsSearchField = new HeSearchField(window);
            m_ObjectsSearchField.downOrUpArrowKeyPressed += m_ObjectsControl.SetFocusAndEnsureSelectedItem;
            m_ObjectsControl.findPressed += m_ObjectsSearchField.SetFocus;

            m_SplitterHorzPropertyGrid = EditorPrefs.GetFloat(GetPrefsKey(() => m_SplitterHorzPropertyGrid), m_SplitterHorzPropertyGrid);
            m_SplitterVertConnections = EditorPrefs.GetFloat(GetPrefsKey(() => m_SplitterVertConnections), m_SplitterVertConnections);
            m_SplitterVertRootPath = EditorPrefs.GetFloat(GetPrefsKey(() => m_SplitterVertRootPath), m_SplitterVertRootPath);

            var job = new Job();
            job.snapshot = snapshot;
            job.control = m_ObjectsControl;
            ScheduleJob(job);
        }

        protected override void OnHide()
        {
            base.OnHide();

            m_ObjectsControl.SaveLayout();

            EditorPrefs.SetFloat(GetPrefsKey(() => m_SplitterHorzPropertyGrid), m_SplitterHorzPropertyGrid);
            EditorPrefs.SetFloat(GetPrefsKey(() => m_SplitterVertConnections), m_SplitterVertConnections);
            EditorPrefs.SetFloat(GetPrefsKey(() => m_SplitterVertRootPath), m_SplitterVertRootPath);
        }

        public override GotoCommand GetRestoreCommand() => 
            m_Selected.valueOut(out var selected) ? new GotoCommand(selected) : base.GetRestoreCommand();

        void OnListViewSelectionChange(PackedManagedObject? item)
        {
            m_Selected = None._;
            if (!item.HasValue)
            {
                m_RootPathView.Clear();
                m_ConnectionsView.Clear();
                m_PropertyGridView.Clear();
                return;
            }

            var selected = new RichManagedObject(snapshot, item.Value.managedObjectsArrayIndex);
            m_Selected = Some(selected);
            m_PropertyGridView.Inspect(selected.packed);
            m_ConnectionsView.Inspect(selected.packed);
            m_RootPathView.Inspect(selected.packed);
        }

        public override void OnGUI()
        {
            base.OnGUI();

            EditorGUI.BeginDisabledGroup(m_ObjectsControl.progress.value < 1);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            var text =
                                $"{m_ObjectsControl.managedObjectsCount} managed object duplicate(s) wasting {EditorUtility.FormatBytes(m_ObjectsControl.managedObjectsSize)} memory";
                            window.SetStatusbarString(text);

                            EditorGUILayout.LabelField(titleContent, EditorStyles.boldLabel);
                            if (m_ObjectsSearchField.OnToolbarGUI())
                                m_ObjectsControl.Search(m_ObjectsSearchField.text);
                        }
                        GUILayout.Space(2);

                        m_ObjectsControl.OnGUI();
                    }

                    m_SplitterVertConnections = HeEditorGUILayout.VerticalSplitter("m_splitterVertConnections".GetHashCode(), m_SplitterVertConnections, 0.1f, 0.8f, window);

                    using (new EditorGUILayout.HorizontalScope(GUILayout.Height(window.position.height * m_SplitterVertConnections)))
                    {
                        m_ConnectionsView.OnGUI();
                    }
                }

                m_SplitterHorzPropertyGrid = HeEditorGUILayout.HorizontalSplitter("m_splitterHorzPropertyGrid".GetHashCode(), m_SplitterHorzPropertyGrid, 0.1f, 0.6f, window);

                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel, GUILayout.Width(window.position.width * m_SplitterHorzPropertyGrid)))
                    {
                        m_PropertyGridView.OnGUI();
                    }

                    m_SplitterVertRootPath = HeEditorGUILayout.VerticalSplitter("m_splitterVertRootPath".GetHashCode(), m_SplitterVertRootPath, 0.1f, 0.8f, window);

                    using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel, GUILayout.Width(window.position.width * m_SplitterHorzPropertyGrid), GUILayout.Height(window.position.height * m_SplitterVertRootPath)))
                    {
                        m_RootPathView.OnGUI();
                    }
                }
            }
            EditorGUI.EndDisabledGroup();

            if (m_ObjectsControl.progress.value < 1)
            {
                window.SetBusy($"Analyzing Managed Objects Memory, {m_ObjectsControl.progress.value * 100:F0}% done");
            }
        }

        class Job : AbstractThreadJob
        {
            public ManagedObjectDuplicatesControl control;
            public PackedMemorySnapshot snapshot;

            // Output
            TreeViewItem tree;

            public override void ThreadFunc()
            {
                tree = control.BuildTree(snapshot);
            }

            public override void IntegrateFunc()
            {
                control.SetTree(tree);
            }
        }
    }
}
