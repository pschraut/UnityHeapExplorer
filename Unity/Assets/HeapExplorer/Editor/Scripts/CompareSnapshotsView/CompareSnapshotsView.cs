using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace HeapExplorer
{
    public class CompareSnapshotsView : HeapExplorerView
    {
        string m_editorPrefsKey;
        CompareSnapshotsControl m_objects;
        HeSearchField m_objectsSearch;
        string m_snapshotBPath = "";
        PackedMemorySnapshot m_snapshotB;
        Job m_job;

        public override void Awake()
        {
            base.Awake();

            title = new GUIContent("Compare Snapshot", "");
            m_editorPrefsKey = "HeapExplorer.CompareSnapshotsView";
        }

        public override void OnDestroy()
        {
            m_job = null;

            base.OnDestroy();
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            m_objects = new CompareSnapshotsControl(m_editorPrefsKey + ".m_objects", new TreeViewState());
            m_objects.gotoCB += Goto;

            m_objectsSearch = new HeSearchField(window);
            m_objectsSearch.downOrUpArrowKeyPressed += m_objects.SetFocusAndEnsureSelectedItem;
            m_objects.findPressed += m_objectsSearch.SetFocus;

            if (m_snapshot != null && m_snapshotB != null)
            {
                var tree = m_objects.BuildTree(m_snapshot, m_snapshotB);
                m_objects.SetTree(tree);
            }
        }

        protected override void OnHide()
        {
            base.OnHide();

            m_objects.SaveLayout();
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

                            if (m_objectsSearch.OnToolbarGUI())
                                m_objects.Search(m_objectsSearch.text);
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
                                    if (!string.IsNullOrEmpty(m_snapshotBPath))
                                    {
                                        if(m_snapshotBPath.EndsWith(".heap", System.StringComparison.OrdinalIgnoreCase))
                                            GUILayout.Label(System.IO.Path.GetFileName(m_snapshotBPath));
                                        else
                                            GUILayout.Label(m_snapshotBPath);
                                    }

                                    if (GUILayout.Button(new GUIContent("Load...", "Load snapshot (B)"), GUILayout.Width(64)))
                                    {
                                        var menu = new GenericMenu();
                                        menu.AddItem(new GUIContent("Browse..."), false, delegate ()
                                        {
                                            var path = EditorUtility.OpenFilePanel("Load", "", "heap");
                                            if (!string.IsNullOrEmpty(path))
                                            {
                                                MruFiles.AddPath(path);
                                                LoadSnapshotB(path);
                                            }
                                        });

                                        menu.AddSeparator("");

                                        for (int n = 0; n < MruFiles.count; ++n)
                                        {
                                            var path = MruFiles.GetPath(n);

                                            if (string.IsNullOrEmpty(path))
                                                continue;

                                            if (!System.IO.File.Exists(path))
                                                continue;

                                            menu.AddItem(new GUIContent((n + 1) + "     " + path.Replace('/', '\\')), false, delegate (System.Object obj)
                                             {
                                                 var p = obj as string;
                                                 MruFiles.AddPath(p);
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

                        m_objects.OnGUI();
                    }
                }
            }

            if (m_job != null)
            {
                window.SetBusy(m_job.stateString);
                Repaint();
            }
        }

        void SwapSnapshots()
        {
            var a = window.snapshot;
            var apath = window.snapshotPath;
            window.snapshot = m_snapshotB;
            window.snapshotPath = m_snapshotBPath;
            m_snapshotB = a;
            m_snapshotBPath = apath;
            m_objects.SwapAB();
        }

        void LoadSnapshotB(string path)
        {
            m_snapshotBPath = path;

            m_job = new Job
            {
                snapshotA = m_snapshot,
                control = m_objects,
                pathB = path,
                view = this
            };
            ScheduleJob(m_job);
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
                        return snapshotB.stateString;
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

                view.m_snapshotBPath = pathB;
                view.m_snapshotB = snapshotB;
                view.m_job = null;
            }
        }
    }
}
