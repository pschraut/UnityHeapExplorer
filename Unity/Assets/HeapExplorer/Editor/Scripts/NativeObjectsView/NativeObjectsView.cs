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
            m_EditorPrefsKey = "HeapExplorer.NativeObjectDuplicatesView";
        }

        protected override void OnRebuild()
        {
            base.OnRebuild();

            m_job = new Job();
            m_job.control = m_NativeObjectsControl;
            m_job.snapshot = m_snapshot;
            m_job.buildArgs.addAssetObjects = this.showAssets;
            m_job.buildArgs.addSceneObjects = this.showSceneObjects;
            m_job.buildArgs.addRuntimeObjects = this.showRuntimeObjects;
            m_job.buildArgs.addDestroyOnLoad = this.showDestroyOnLoadObjects;
            m_job.buildArgs.addDontDestroyOnLoad = this.showDontDestroyOnLoadObjects;
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
            var text = string.Format("{0} native UnityEngine object guessed duplicate(s) wasting {1} memory", m_NativeObjectsControl.nativeObjectsCount, EditorUtility.FormatBytes(m_NativeObjectsControl.nativeObjectsSize));
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
            public NativeObjectsControl.BuildArgs buildArgs;

            // Output
            TreeViewItem tree;

            public override void ThreadFunc()
            {
                tree = control.BuildDuplicatesTree(snapshot, buildArgs);
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
            m_EditorPrefsKey = "HeapExplorer.NativeObjectsView";
        }

        protected override void OnRebuild()
        {
            base.OnRebuild();

            m_job = new Job();
            m_job.control = m_NativeObjectsControl;
            m_job.snapshot = m_snapshot;
            m_job.buildArgs.addAssetObjects = this.showAssets;
            m_job.buildArgs.addSceneObjects = this.showSceneObjects;
            m_job.buildArgs.addRuntimeObjects = this.showRuntimeObjects;
            m_job.buildArgs.addDestroyOnLoad = this.showDestroyOnLoadObjects;
            m_job.buildArgs.addDontDestroyOnLoad = this.showDontDestroyOnLoadObjects;
            ScheduleJob(m_job);
        }

        protected override void OnDrawHeader()
        {
            base.OnDrawHeader();

            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            //var text = string.Format("{0} native UnityEngine object(s)", m_snapshot.nativeObjects.Length);
            var text = string.Format("{0} native UnityEngine object(s) using {1} memory", m_NativeObjectsControl.nativeObjectsCount, EditorUtility.FormatBytes(m_NativeObjectsControl.nativeObjectsSize));
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
            public NativeObjectsControl.BuildArgs buildArgs;

            // Output
            TreeViewItem tree;

            public override void ThreadFunc()
            {
                tree = control.BuildTree(snapshot, buildArgs);
            }

            public override void IntegrateFunc()
            {
                control.SetTree(tree);
            }
        }
    }

    public class AbstractNativeObjectsView : HeapExplorerView
    {
        protected string m_EditorPrefsKey;
        protected NativeObjectsControl m_NativeObjectsControl;

        NativeObjectControl m_NativeObjectControl;
        HeSearchField m_SearchField;
        ConnectionsView m_ConnectionsView;
        PackedNativeUnityEngineObject? m_Selected;
        RootPathView m_RootPathView;
        float m_SplitterHorz = 0.33333f;
        float m_SplitterVert = 0.32f;

        protected bool showAssets
        {
            get
            {
                return EditorPrefs.GetBool(m_EditorPrefsKey + ".showAssets", true);
            }
            set
            {
                EditorPrefs.SetBool(m_EditorPrefsKey + ".showAssets", value);
            }
        }

        protected bool showSceneObjects
        {
            get
            {
                return EditorPrefs.GetBool(m_EditorPrefsKey + ".showSceneObjects", true);
            }
            set
            {
                EditorPrefs.SetBool(m_EditorPrefsKey + ".showSceneObjects", value);
            }
        }

        protected bool showRuntimeObjects
        {
            get
            {
                return EditorPrefs.GetBool(m_EditorPrefsKey + ".showRuntimeObjects", true);
            }
            set
            {
                EditorPrefs.SetBool(m_EditorPrefsKey + ".showRuntimeObjects", value);
            }
        }

        protected bool showDestroyOnLoadObjects
        {
            get
            {
                return EditorPrefs.GetBool(m_EditorPrefsKey + ".showDestroyOnLoadObjects", true);
            }
            set
            {
                EditorPrefs.SetBool(m_EditorPrefsKey + ".showDestroyOnLoadObjects", value);
            }
        }

        protected bool showDontDestroyOnLoadObjects
        {
            get
            {
                return EditorPrefs.GetBool(m_EditorPrefsKey + ".showDontDestroyOnLoadObjects", true);
            }
            set
            {
                EditorPrefs.SetBool(m_EditorPrefsKey + ".showDontDestroyOnLoadObjects", value);
            }
        }

        public override void Awake()
        {
            base.Awake();

            hasMainMenu = true;
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            m_ConnectionsView = CreateView<ConnectionsView>();
            m_ConnectionsView.editorPrefsKey = m_EditorPrefsKey + ".m_connectionsView";

            m_RootPathView = CreateView<RootPathView>();
            m_RootPathView.editorPrefsKey = m_EditorPrefsKey + ".m_rootPathView";

            // The list at the left that contains all native objects
            m_NativeObjectsControl = new NativeObjectsControl(m_EditorPrefsKey + ".m_nativeObjectsControl", new TreeViewState());
            //m_nativeObjectsControl.SetTree(m_nativeObjectsControl.BuildTree(m_snapshot));
            m_NativeObjectsControl.onSelectionChange += OnListViewSelectionChange;
            m_NativeObjectsControl.gotoCB += Goto;

            m_SearchField = new HeSearchField(window);
            m_SearchField.downOrUpArrowKeyPressed += m_NativeObjectsControl.SetFocusAndEnsureSelectedItem;
            m_NativeObjectsControl.findPressed += m_SearchField.SetFocus;

            // The list at the right that shows the selected native object
            m_NativeObjectControl = new NativeObjectControl(m_EditorPrefsKey + ".m_nativeObjectControl", new TreeViewState());

            m_SplitterHorz = EditorPrefs.GetFloat(m_EditorPrefsKey + ".m_splitterHorz", m_SplitterHorz);
            m_SplitterVert = EditorPrefs.GetFloat(m_EditorPrefsKey + ".m_splitterVert", m_SplitterVert);

            OnRebuild();
        }

        protected override void OnHide()
        {
            base.OnHide();

            m_NativeObjectsControl.SaveLayout();
            m_NativeObjectControl.SaveLayout();

            EditorPrefs.SetFloat(m_EditorPrefsKey + ".m_splitterHorz", m_SplitterHorz);
            EditorPrefs.SetFloat(m_EditorPrefsKey + ".m_splitterVert", m_SplitterVert);
        }

        public override GenericMenu CreateMainMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Show assets"), showAssets, OnToggleShowAssets);
            menu.AddItem(new GUIContent("Show scene objects"), showSceneObjects, OnToggleSceneObjects);
            menu.AddItem(new GUIContent("Show runtime objects"), showRuntimeObjects, OnToggleRuntimeObjects);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Show destroy on load objects"), showDestroyOnLoadObjects, OnToggleDestroyOnLoadObjects);
            menu.AddItem(new GUIContent("Show do NOT destroy on load objects"), showDontDestroyOnLoadObjects, OnToggleDontDestroyOnLoadObjects);
            return menu;
        }
        
        protected virtual void OnRebuild()
        {
            // Derived classes overwrite this method to trigger their
            // individual tree rebuild jobs
        }

        void OnToggleShowAssets()
        {
            showAssets = !showAssets;
            OnRebuild();
        }

        void OnToggleSceneObjects()
        {
            showSceneObjects = !showSceneObjects;
            OnRebuild();
        }

        void OnToggleRuntimeObjects()
        {
            showRuntimeObjects = !showRuntimeObjects;
            OnRebuild();
        }

        void OnToggleDestroyOnLoadObjects()
        {
            showDestroyOnLoadObjects = !showDestroyOnLoadObjects;
            OnRebuild();
        }

        void OnToggleDontDestroyOnLoadObjects()
        {
            showDontDestroyOnLoadObjects = !showDontDestroyOnLoadObjects;
            OnRebuild();
        }

        public override GotoCommand GetRestoreCommand()
        {
            var command = m_Selected.HasValue ? new GotoCommand(new RichNativeObject(m_snapshot, m_Selected.Value.nativeObjectsArrayIndex)) : null;
            return command;
        }

        public void Select(PackedNativeUnityEngineObject packed)
        {
            m_NativeObjectsControl.Select(packed);
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
                                m_NativeObjectsControl.Search(m_SearchField.text);
                        }
                        GUILayout.Space(2);

                        m_NativeObjectsControl.OnGUI();
                    }

                    HeEditorGUILayout.VerticalSplitter("m_splitterVert".GetHashCode(), ref m_SplitterVert, 0.1f, 0.8f, window);

                    using (new EditorGUILayout.HorizontalScope(GUILayout.Height(window.position.height * m_SplitterVert)))
                    {
                        m_ConnectionsView.OnGUI();
                    }
                }

                HeEditorGUILayout.HorizontalSplitter("m_splitterHorz".GetHashCode(), ref m_SplitterHorz, 0.1f, 0.8f, window);

                // Various panels at the right side
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(window.position.width * m_SplitterHorz)))
                {
                    using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel, GUILayout.MinHeight(250), GUILayout.MaxHeight(250)))
                    {
                        using (new EditorGUILayout.HorizontalScope(GUILayout.MaxWidth(16)))
                        {
                            if (m_Selected.HasValue)
                                HeEditorGUI.NativeObjectIcon(GUILayoutUtility.GetRect(16, 16), m_Selected.Value);

                            EditorGUILayout.LabelField("Native UnityEngine object", EditorStyles.boldLabel);
                        }

                        GUILayout.Space(2);
                        m_NativeObjectControl.OnGUI();
                    }

                    using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel))
                    {
                        m_RootPathView.OnGUI();
                    }
                }
            }
        }

        protected virtual void OnDrawHeader()
        {
        }

        void OnListViewSelectionChange(PackedNativeUnityEngineObject? nativeObject)
        {
            m_Selected = nativeObject;
            if (!m_Selected.HasValue)
            {
                m_RootPathView.Clear();
                m_ConnectionsView.Clear();
                m_NativeObjectControl.Clear();
                return;
            }

            m_RootPathView.Inspect(m_Selected.Value);
            m_ConnectionsView.Inspect(m_Selected.Value);
            m_NativeObjectControl.Inspect(m_snapshot, m_Selected.Value);
        }
    }
}
