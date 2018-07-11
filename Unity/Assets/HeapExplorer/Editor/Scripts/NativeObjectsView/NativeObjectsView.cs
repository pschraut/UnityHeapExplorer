using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;

namespace HeapExplorer
{
    public class NativeObjectsView : AbstractNativeObjectsView
    {
        Job m_job;

        [InitializeOnLoadMethod]
        static void Register()
        {
            HeapExplorerWindow.Register<NativeObjectsView>();
        }

        public override void Awake()
        {
            base.Awake();

            titleContent = new GUIContent("C++ Objects", "");
            m_EditorPrefsKey = "HeapExplorer.NativeObjectsView";
        }

        public override int CanProcessCommand(GotoCommand command)
        {
            if (command.toNativeObject.isValid)
                return 10;

            return base.CanProcessCommand(command);
        }

        protected override void OnRebuild()
        {
            base.OnRebuild();

            m_job = new Job();
            m_job.control = m_NativeObjectsControl;
            m_job.snapshot = snapshot;
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

            EditorGUILayout.LabelField(titleContent, EditorStyles.boldLabel);

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
}
