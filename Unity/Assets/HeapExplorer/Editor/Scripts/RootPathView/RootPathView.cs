using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;

namespace HeapExplorer
{
    public class RootPathView : HeapExplorerView
    {
        RootPath m_selected;
        RootPathControl m_rootPathControl;
        RootPathUtility m_rootPaths = new RootPathUtility();

        public string editorPrefsKey
        {
            get;
            set;
        }

        public override void Awake()
        {
            base.Awake();

            editorPrefsKey = "HeapExplorer.RootPathView";
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            m_rootPathControl = new RootPathControl(editorPrefsKey + ".m_rootPathControl", new TreeViewState());
            m_rootPathControl.gotoCB += Goto;
            m_rootPathControl.onSelectionChange += OnSelectionChange;
        }

        protected override void OnHide()
        {
            base.OnHide();

            m_rootPathControl.SaveLayout();
        }

        public override void OnGUI()
        {
            base.OnGUI();

            using (new EditorGUILayout.VerticalScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(string.Format("{0} Path(s) to Root", m_rootPaths.count), EditorStyles.boldLabel);
                }

                GUILayout.Space(2);
                m_rootPathControl.OnGUI();

                GUILayout.Space(2);
                var reason = "No root object selected.";
                if (m_selected != null)
                    reason = m_selected.reasonString;
                EditorGUI.HelpBox(GUILayoutUtility.GetRect(10, 48, GUILayout.ExpandWidth(true)), reason, MessageType.Info);
            }
        }

        public void Inspect(PackedNativeUnityEngineObject item)
        {
            m_selected = null;
            ScheduleJob(new ObjectProxy(m_snapshot, item));
        }

        public void Inspect(PackedManagedObject item)
        {
            m_selected = null;
            ScheduleJob(new ObjectProxy(m_snapshot, item));
        }

        public void Inspect(PackedManagedStaticField item)
        {
            m_selected = null;
            ScheduleJob(new ObjectProxy(m_snapshot, item));
        }

        public void Inspect(PackedGCHandle item)
        {
            m_selected = null;
            ScheduleJob(new ObjectProxy(m_snapshot, item));
        }

        public void Clear()
        {
            m_selected = null;
            m_rootPaths = new RootPathUtility();
            ScheduleJob(new RootPathJob() { control = m_rootPathControl });
        }

        void ScheduleJob(ObjectProxy objectProxy)
        {
            var job = new RootPathJob
            {
                snapshot = m_snapshot,
                objectProxy = objectProxy,
                control = m_rootPathControl,
                paths = m_rootPaths = new RootPathUtility()
            };
            ScheduleJob(job);
        }

        void OnSelectionChange(RootPath path)
        {
            m_selected = path;
        }

        class RootPathJob : AbstractThreadJob
        {
            public ObjectProxy objectProxy;
            public RootPathControl control;
            public PackedMemorySnapshot snapshot;

            // in/out
            public RootPathUtility paths;

            // Output
            public TreeViewItem tree;

            public override void ThreadFunc()
            {
                if (objectProxy != null)
                    paths.Find(objectProxy);

                tree = control.BuildTree(snapshot, paths);
            }

            public override void IntegrateFunc()
            {
                control.SetTree(tree);
            }
        }
    }
}
