using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;

namespace HeapExplorer
{
    public class RootPathView : HeapExplorerView
    {
        RootPath m_Selected;
        RootPathControl m_RootPathControl;
        RootPathUtility m_RootPaths = new RootPathUtility();

        protected override void OnCreate()
        {
            base.OnCreate();

            m_RootPathControl = new RootPathControl(window, GetPrefsKey(() => m_RootPathControl), new TreeViewState());
            m_RootPathControl.onSelectionChange += OnSelectionChange;
        }

        protected override void OnHide()
        {
            base.OnHide();

            m_RootPathControl.SaveLayout();
        }

        public override void OnGUI()
        {
            base.OnGUI();

            using (new EditorGUILayout.VerticalScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(string.Format("{0} Path(s) to Root", m_RootPaths.count), EditorStyles.boldLabel);
                }

                GUILayout.Space(2);
                m_RootPathControl.OnGUI();

                GUILayout.Space(2);
                var reason = "No root object selected.";
                if (m_Selected != null)
                    reason = m_Selected.reasonString;
                EditorGUI.HelpBox(GUILayoutUtility.GetRect(10, 48, GUILayout.ExpandWidth(true)), reason, MessageType.Info);
            }
        }

        public void Inspect(PackedNativeUnityEngineObject item)
        {
            m_Selected = null;
            ScheduleJob(new ObjectProxy(snapshot, item));
        }

        public void Inspect(PackedManagedObject item)
        {
            m_Selected = null;
            ScheduleJob(new ObjectProxy(snapshot, item));
        }

        public void Inspect(PackedManagedStaticField item)
        {
            m_Selected = null;
            ScheduleJob(new ObjectProxy(snapshot, item));
        }

        public void Inspect(PackedGCHandle item)
        {
            m_Selected = null;
            ScheduleJob(new ObjectProxy(snapshot, item));
        }

        public void Clear()
        {
            m_Selected = null;
            m_RootPaths = new RootPathUtility();
            ScheduleJob(new RootPathJob() { control = m_RootPathControl });
        }

        void ScheduleJob(ObjectProxy objectProxy)
        {
            var job = new RootPathJob
            {
                snapshot = snapshot,
                objectProxy = objectProxy,
                control = m_RootPathControl,
                paths = m_RootPaths = new RootPathUtility()
            };
            ScheduleJob(job);
        }

        void OnSelectionChange(RootPath path)
        {
            m_Selected = path;
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
