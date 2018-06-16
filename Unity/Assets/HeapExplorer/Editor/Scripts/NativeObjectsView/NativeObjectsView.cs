using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;

namespace HeapExplorer
{
    public class NativeObjectDuplicatesView : AbstractNativeObjectsView
    {
        // https://docs.unity3d.com/Manual/BestPracticeUnderstandingPerformanceInUnity2.html

        Job m_job;

        public override void Awake()
        {
            base.Awake();

            title = new GUIContent("C++ Asset Duplicates (guessed)", "");
            m_editorPrefsKey = "HeapExplorer.NativeObjectDuplicatesView";
        }


        protected override void OnCreate()
        {
            base.OnCreate();
            m_job = new Job();
            m_job.control = m_nativeObjectsControl;
            m_job.snapshot = m_snapshot;
            ScheduleJob(m_job);
        }

        protected override void OnDrawHeader()
        {
            base.OnDrawHeader();

            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

                var url = "https://docs.unity3d.com/Manual/BestPracticeUnderstandingPerformanceInUnity2.html";
                if (HeEditorGUILayout.LinkButton(new GUIContent("See 'Identifying duplicated Textures' in Unity documentation.", url), HeEditorStyles.miniHyperlink))
                    Application.OpenURL(url);
            }

            //var text = string.Format("{0} native UnityEngine object(s)", m_snapshot.nativeObjects.Length);
            var text = string.Format("{0} native UnityEngine object guessed duplicate(s) wasting {1} memory", m_nativeObjectsControl.nativeObjectsCount, EditorUtility.FormatBytes(m_nativeObjectsControl.nativeObjectsSize));
            window.SetStatusbarString(text);
        }

        public override void OnGUI()
        {
            base.OnGUI();

            if (m_job != null && m_job.state != AbstractThreadJob.State.Completed)
                window.SetBusy("Working...");
            else if (m_job != null && m_job.state == AbstractThreadJob.State.Completed)
                m_job = null;
        }

        class Job : AbstractThreadJob
        {
            public NativeObjectsControl control;
            public PackedMemorySnapshot snapshot;

            // Output
            TreeViewItem tree;

            public override void ThreadFunc()
            {
                tree = control.BuildDuplicatesTree(snapshot);
            }

            public override void IntegrateFunc()
            {
                control.SetTree(tree);
            }
        }
    }

    public class NativeObjectsView : AbstractNativeObjectsView
    {
        Job m_job;

        public override void Awake()
        {
            base.Awake();

            title = new GUIContent("C++ Objects", "");
            m_editorPrefsKey = "HeapExplorer.NativeObjectsView";
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            m_job = new Job();
            m_job.control = m_nativeObjectsControl;
            m_job.snapshot = m_snapshot;
            ScheduleJob(m_job);
        }

        protected override void OnDrawHeader()
        {
            base.OnDrawHeader();

            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            var text = string.Format("{0} native UnityEngine object(s)", m_snapshot.nativeObjects.Length);
            window.SetStatusbarString(text);
        }

        public override void OnGUI()
        {
            base.OnGUI();

            if (m_job != null && m_job.state != AbstractThreadJob.State.Completed)
                window.SetBusy("Working...");
            else if (m_job != null && m_job.state == AbstractThreadJob.State.Completed)
                m_job = null;
        }

        class Job : AbstractThreadJob
        {
            public NativeObjectsControl control;
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

    public class AbstractNativeObjectsView : HeapExplorerView
    {
        protected string m_editorPrefsKey;

        protected NativeObjectsControl m_nativeObjectsControl;
        NativeObjectControl m_nativeObjectControl;
        HeSearchField m_SearchField;
        ConnectionsView m_connectionsView;
        PackedNativeUnityEngineObject? m_selected;
        RootPathView m_rootPathView;
        float m_splitterHorz = 0.33333f;
        float m_splitterVert = 0.32f;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_connectionsView = CreateView<ConnectionsView>();
            m_connectionsView.editorPrefsKey = m_editorPrefsKey + ".m_connectionsView";

            m_rootPathView = CreateView<RootPathView>();
            m_rootPathView.editorPrefsKey = m_editorPrefsKey + ".m_rootPathView";

            // The list at the left that contains all native objects
            m_nativeObjectsControl = new NativeObjectsControl(m_editorPrefsKey + ".m_nativeObjectsControl", new TreeViewState());
            //m_nativeObjectsControl.SetTree(m_nativeObjectsControl.BuildTree(m_snapshot));
            m_nativeObjectsControl.onSelectionChange += OnListViewSelectionChange;
            m_nativeObjectsControl.gotoCB += Goto;

            m_SearchField = new HeSearchField(window);
            m_SearchField.downOrUpArrowKeyPressed += m_nativeObjectsControl.SetFocusAndEnsureSelectedItem;
            m_nativeObjectsControl.findPressed += m_SearchField.SetFocus;

            // The list at the right that shows the selected native object
            m_nativeObjectControl = new NativeObjectControl(m_editorPrefsKey + ".m_nativeObjectControl", new TreeViewState());

            m_splitterHorz = EditorPrefs.GetFloat(m_editorPrefsKey + ".m_splitterHorz", m_splitterHorz);
            m_splitterVert = EditorPrefs.GetFloat(m_editorPrefsKey + ".m_splitterVert", m_splitterVert);
        }

        protected override void OnHide()
        {
            base.OnHide();

            m_nativeObjectsControl.SaveLayout();
            m_nativeObjectControl.SaveLayout();

            EditorPrefs.SetFloat(m_editorPrefsKey + ".m_splitterHorz", m_splitterHorz);
            EditorPrefs.SetFloat(m_editorPrefsKey + ".m_splitterVert", m_splitterVert);
        }

        public override GotoCommand GetRestoreCommand()
        {
            var command = m_selected.HasValue ? new GotoCommand(new RichNativeObject(m_snapshot, m_selected.Value.nativeObjectsArrayIndex)) : null;
            return command;
        }

        public void Select(PackedNativeUnityEngineObject packed)
        {
            m_nativeObjectsControl.Select(packed);
        }

        public override void OnGUI()
        {
            base.OnGUI();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    // Native objects list at the left side
                    using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            OnDrawHeader();
                            //var text = string.Format("{0} native UnityEngine object(s)", m_snapshot.nativeObjects.Length);
                            //window.SetStatusbarString(text);
                            //EditorGUILayout.LabelField(string.Format("{0} native UnityEngine object(s)", m_snapshot.nativeObjects.Length), EditorStyles.boldLabel);

                            
                            if (m_SearchField.OnToolbarGUI())
                                m_nativeObjectsControl.Search(m_SearchField.text);
                        }
                        GUILayout.Space(2);

                        m_nativeObjectsControl.OnGUI();
                    }

                    HeEditorGUILayout.VerticalSplitter("m_splitterVert".GetHashCode(), ref m_splitterVert, 0.1f, 0.8f, window);

                    using (new EditorGUILayout.HorizontalScope(GUILayout.Height(window.position.height * m_splitterVert)))
                    {
                        m_connectionsView.OnGUI();
                    }
                }

                HeEditorGUILayout.HorizontalSplitter("m_splitterHorz".GetHashCode(), ref m_splitterHorz, 0.1f, 0.8f, window);

                // Various panels at the right side
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(window.position.width * m_splitterHorz)))
                {
                    using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel, GUILayout.MinHeight(250), GUILayout.MaxHeight(250)))
                    {
                        EditorGUILayout.LabelField("Native UnityEngine object", EditorStyles.boldLabel);
                        GUILayout.Space(2);
                        m_nativeObjectControl.OnGUI();
                    }

                    using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel))
                    {
                        m_rootPathView.OnGUI();
                    }
                }
            }
        }

        protected virtual void OnDrawHeader()
        {
        }

        void OnListViewSelectionChange(PackedNativeUnityEngineObject? nativeObject)
        {
            m_selected = nativeObject;
            if (!m_selected.HasValue)
            {
                m_rootPathView.Clear();
                m_connectionsView.Clear();
                m_nativeObjectControl.Clear();
                return;
            }

            m_rootPathView.Inspect(m_selected.Value);
            m_connectionsView.Inspect(m_selected.Value);
            m_nativeObjectControl.Inspect(m_snapshot, m_selected.Value);
        }
    }
}
