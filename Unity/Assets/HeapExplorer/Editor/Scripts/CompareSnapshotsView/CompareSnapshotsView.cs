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
    public class CompareSnapshotsView : HeapExplorerView
    {
        CompareSnapshotsControl m_CompareControl;
        HeSearchField m_CompareSearchField;
        string m_SnapshotBPath = "";
        PackedMemorySnapshot m_SnapshotB;
        Job m_Job;

        [InitializeOnLoadMethod]
        static void Register()
        {
            HeapExplorerWindow.Register<CompareSnapshotsView>();
        }

        public override void Awake()
        {
            base.Awake();

            titleContent = new GUIContent("Compare Snapshot", "");
            viewMenuOrder = 850;
        }

        public override void OnDestroy()
        {
            m_Job = null;

            base.OnDestroy();
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            m_CompareControl = new CompareSnapshotsControl(window, GetPrefsKey(()=> m_CompareControl), new TreeViewState());

            m_CompareSearchField = new HeSearchField(window);
            m_CompareSearchField.downOrUpArrowKeyPressed += m_CompareControl.SetFocusAndEnsureSelectedItem;
            m_CompareControl.findPressed += m_CompareSearchField.SetFocus;

            if (snapshot != null && m_SnapshotB != null)
            {
                var tree = m_CompareControl.BuildTree(snapshot, m_SnapshotB);
                m_CompareControl.SetTree(tree);
            }
        }

        protected override void OnHide()
        {
            base.OnHide();

            m_CompareControl.SaveLayout();
        }

        public override void OnGUI()
        {
            base.OnGUI();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField("Compare Snapshot (A) with (B)", EditorStyles.boldLabel);

                            if (m_CompareSearchField.OnToolbarGUI())
                                m_CompareControl.Search(m_CompareSearchField.text);
                        }

                        GUILayout.Space(2);

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button(new GUIContent("Swap", "Swap snapshot A <> B"), GUILayout.Width(64), GUILayout.ExpandHeight(true)))
                            {
                                SwapSnapshots();
                            }

                            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                            {
                                using (new EditorGUILayout.HorizontalScope())
                                {
                                    GUILayout.Label("(A)", GUILayout.Width(24));

                                    if (!string.IsNullOrEmpty(window.snapshotPath))
                                    {
                                        if (window.snapshotPath.EndsWith(".heap", System.StringComparison.OrdinalIgnoreCase))
                                            GUILayout.Label(System.IO.Path.GetFileName(window.snapshotPath));
                                        else
                                            GUILayout.Label(window.snapshotPath);
                                    }

                                    GUILayout.FlexibleSpace();
                                }

                                using (new EditorGUILayout.HorizontalScope())
                                {
                                    GUILayout.Label("(B)", GUILayout.Width(24));
                                    if (!string.IsNullOrEmpty(m_SnapshotBPath))
                                    {
                                        if(m_SnapshotBPath.EndsWith(".heap", System.StringComparison.OrdinalIgnoreCase))
                                            GUILayout.Label(System.IO.Path.GetFileName(m_SnapshotBPath));
                                        else
                                            GUILayout.Label(m_SnapshotBPath);
                                    }

                                    if (GUILayout.Button(new GUIContent("Load...", "Load snapshot (B)"), GUILayout.Width(64)))
                                    {
                                        var menu = new GenericMenu();
                                        menu.AddItem(new GUIContent("Browse..."), false, delegate ()
                                        {
                                            var path = EditorUtility.OpenFilePanel("Load", "", "heap");
                                            if (!string.IsNullOrEmpty(path))
                                            {
                                                HeMruFiles.AddPath(path);
                                                LoadSnapshotB(path);
                                            }
                                        });

                                        menu.AddSeparator("");

                                        for (int n = 0; n < HeMruFiles.count; ++n)
                                        {
                                            var path = HeMruFiles.GetPath(n);

                                            if (string.IsNullOrEmpty(path))
                                                continue;

                                            if (!System.IO.File.Exists(path))
                                                continue;

                                            menu.AddItem(new GUIContent((n + 1) + "     " + path.Replace('/', '\\')), false, delegate (System.Object obj)
                                             {
                                                 var p = obj as string;
                                                 HeMruFiles.AddPath(p);
                                                 LoadSnapshotB(p);
                                             }, path);
                                        }

                                        menu.ShowAsContext();
                                    }

                                    GUILayout.FlexibleSpace();
                                }

                                GUILayout.Space(2);
                            }
                        }

                        GUILayout.Space(2);

                        m_CompareControl.OnGUI();
                    }
                }
            }

            if (m_Job != null)
            {
                window.SetBusy(m_Job.stateString);
                Repaint();
            }
        }

        void SwapSnapshots()
        {
            var a = window.snapshot;
            var apath = window.snapshotPath;
            window.snapshot = m_SnapshotB;
            window.snapshotPath = m_SnapshotBPath;
            m_SnapshotB = a;
            m_SnapshotBPath = apath;
            m_CompareControl.SwapAB();
        }

        void LoadSnapshotB(string path)
        {
            m_SnapshotBPath = path;

            m_Job = new Job
            {
                snapshotA = snapshot,
                control = m_CompareControl,
                pathB = path,
                view = this
            };
            ScheduleJob(m_Job);
        }

        class Job : AbstractThreadJob
        {
            public CompareSnapshotsControl control;
            public PackedMemorySnapshot snapshotA;
            public string pathB;
            public CompareSnapshotsView view;

            public string stateString
            {
                get
                {
                    if (snapshotB != null)
                        return snapshotB.busyString;
                    return "<null> snapshot";
                }
            }

            // Output
            PackedMemorySnapshot snapshotB;
            TreeViewItem tree;

            public override void ThreadFunc()
            {
                snapshotB = new PackedMemorySnapshot();
                snapshotB.LoadFromFile(pathB);
                snapshotB.Initialize();
                tree = control.BuildTree(snapshotA, snapshotB);
            }

            public override void IntegrateFunc()
            {
                control.SetTree(tree);

                view.m_SnapshotBPath = pathB;
                view.m_SnapshotB = snapshotB;
                view.m_Job = null;
            }
        }
    }
}
