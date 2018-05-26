using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using System;

namespace HeapExplorer
{
    // A large number of objects with the same value is inefficient from the point of memory usage.
    public class ManagedObjectDuplicatesView : HeapExplorerView
    {
        string m_editorPrefsKey;
        ManagedObjectDuplicatesControl m_objects;
        HeSearchField m_objectsSearch;
        ConnectionsView m_connectionsView;
        RichManagedObject m_selected;
        RootPathView m_rootPathView;
        PropertyGridView m_propertyGridView;
        float m_splitterHorzPropertyGrid = 0.32f;
        float m_splitterVertConnections = 0.3333f;
        float m_splitterVertRootPath = 0.3333f;

        public override void Awake()
        {
            base.Awake();

            title = new GUIContent("C# Object Duplicates", "");
            m_editorPrefsKey = "HeapExplorer.ManagedObjectDuplicatesView";
        }
        
        void DrawLoading()
        {
            var oldColor = GUI.color;
            var oldMatrix = GUI.matrix;
            var iconSize = 128;

            var pivotPoint = new Vector2(window.position.width / 2, window.position.height / 2);
            GUIUtility.RotateAroundPivot(Time.realtimeSinceStartup * 45, pivotPoint);

            var r = new Rect(pivotPoint - new Vector2(0.5f, 0.5f) * iconSize, Vector2.one * iconSize);
            GUI.color = new Color(1, 1, 1, 1.0f);
            GUI.DrawTexture(r, HeEditorStyles.loadingImageBig);

            GUI.matrix = oldMatrix;
            GUI.color = oldColor;

            r = new Rect(new Vector2(0, pivotPoint.y - iconSize), new Vector2(window.position.width, iconSize * 0.5f));
            GUI.Label(r, string.Format("Analyzing Managed Objects Memory, {0:F0}% done", m_objects.progress.value*100), HeEditorStyles.loadingLabel);
        }


        protected override void OnCreate()
        {
            base.OnCreate();

            m_connectionsView = CreateView<ConnectionsView>();
            m_connectionsView.editorPrefsKey = m_editorPrefsKey + ".m_connectionsView";

            m_rootPathView = CreateView<RootPathView>();
            m_rootPathView.editorPrefsKey = m_editorPrefsKey + ".m_rootPathView";

            m_propertyGridView = CreateView<PropertyGridView>();
            m_propertyGridView.editorPrefsKey = m_editorPrefsKey + ".m_propertyGridView";

            m_objects = new ManagedObjectDuplicatesControl(m_editorPrefsKey + ".m_objects", new TreeViewState());
            m_objects.onSelectionChange += OnListViewSelectionChange;
            m_objects.gotoCB += Goto;

            m_objectsSearch = new HeSearchField(window);
            m_objectsSearch.downOrUpArrowKeyPressed += m_objects.SetFocusAndEnsureSelectedItem;
            m_objects.findPressed += m_objectsSearch.SetFocus;

            m_splitterHorzPropertyGrid = EditorPrefs.GetFloat(m_editorPrefsKey + ".m_splitterHorzPropertyGrid", m_splitterHorzPropertyGrid);
            m_splitterVertConnections = EditorPrefs.GetFloat(m_editorPrefsKey + ".m_splitterVertConnections", m_splitterVertConnections);
            m_splitterVertRootPath = EditorPrefs.GetFloat(m_editorPrefsKey + ".m_splitterVertRootPath", m_splitterVertRootPath);

            var job = new Job();
            job.snapshot = m_snapshot;
            job.control = m_objects;
            ScheduleJob(job);
        }
        
        protected override void OnHide()
        {
            base.OnHide();

            m_objects.SaveLayout();

            EditorPrefs.SetFloat(m_editorPrefsKey + ".m_splitterHorzPropertyGrid", m_splitterHorzPropertyGrid);
            EditorPrefs.SetFloat(m_editorPrefsKey + ".m_splitterVertConnections", m_splitterVertConnections);
            EditorPrefs.SetFloat(m_editorPrefsKey + ".m_splitterVertRootPath", m_splitterVertRootPath);
        }

        public override GotoCommand GetRestoreCommand()
        {
            var command = m_selected.isValid ? new GotoCommand(m_selected) { toKind = GotoCommand.EKind.ManagedObjectDuplicate } : null;
            return command;
        }

        public void Select(PackedManagedObject packed)
        {
            m_objects.Select(packed);
        }

        void OnListViewSelectionChange(PackedManagedObject? item)
        {
            m_selected = RichManagedObject.invalid;
            if (!item.HasValue)
            {
                m_rootPathView.Clear();
                m_connectionsView.Clear();
                m_propertyGridView.Clear();
                return;
            }

            m_selected = new RichManagedObject(m_snapshot, item.Value.managedObjectsArrayIndex);
            m_propertyGridView.Inspect(m_selected.packed);
            m_connectionsView.Inspect(m_selected.packed);
            m_rootPathView.Inspect(m_selected.packed);
        }

        public override void OnGUI()
        {
            base.OnGUI();

            EditorGUI.BeginDisabledGroup(m_objects.progress.value < 1);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            var text = string.Format("{0} managed object duplicate(s) wasting {1} memory", m_objects.managedObjectsCount, EditorUtility.FormatBytes(m_objects.managedObjectsSize));
                            window.SetStatusbarString(text);
                            //EditorGUILayout.LabelField(string.Format("{0} managed object duplicate(s) wasting {1} memory", m_objects.managedObjectsCount, EditorUtility.FormatBytes(m_objects.managedObjectsSize)), EditorStyles.boldLabel);

                            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                            if (m_objectsSearch.OnToolbarGUI())
                                m_objects.Search(m_objectsSearch.text);
                        }
                        GUILayout.Space(2);

                        m_objects.OnGUI();
                    }

                    HeEditorGUILayout.VerticalSplitter("m_splitterVertConnections".GetHashCode(), ref m_splitterVertConnections, 0.1f, 0.8f, window);

                    using (new EditorGUILayout.HorizontalScope(GUILayout.Height(window.position.height * m_splitterVertConnections)))
                    {
                        m_connectionsView.OnGUI();
                    }
                }

                HeEditorGUILayout.HorizontalSplitter("m_splitterHorzPropertyGrid".GetHashCode(), ref m_splitterHorzPropertyGrid, 0.1f, 0.6f, window);

                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel, GUILayout.Width(window.position.width * m_splitterHorzPropertyGrid)))
                    {
                        m_propertyGridView.OnGUI();
                    }

                    HeEditorGUILayout.VerticalSplitter("m_splitterVertRootPath".GetHashCode(), ref m_splitterVertRootPath, 0.1f, 0.8f, window);

                    using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel, GUILayout.Width(window.position.width * m_splitterHorzPropertyGrid), GUILayout.Height(window.position.height * m_splitterVertRootPath)))
                    {
                        m_rootPathView.OnGUI();
                    }
                }
            }
            EditorGUI.EndDisabledGroup();

            if (m_objects.progress.value < 1)
            {
                DrawLoading();
                Repaint();
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
