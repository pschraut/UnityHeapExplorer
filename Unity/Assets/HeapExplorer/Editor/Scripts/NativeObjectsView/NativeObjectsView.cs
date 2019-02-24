//
// Heap Explorer for Unity. Copyright (c) 2019 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://bitbucket.org/pschraut/unityheapexplorer/
//
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;

namespace HeapExplorer
{
    public class NativeObjectsView : AbstractNativeObjectsView
    {
        Job m_Job;

        [InitializeOnLoadMethod]
        static void Register()
        {
            HeapExplorerWindow.Register<NativeObjectsView>();
        }

        public override void Awake()
        {
            base.Awake();

            titleContent = new GUIContent("C++ Objects", "");
            viewMenuOrder = 550;
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

            m_Job = new Job();
            m_Job.control = m_NativeObjectsControl;
            m_Job.snapshot = snapshot;
            m_Job.buildArgs.addAssetObjects = this.showAssets;
            m_Job.buildArgs.addSceneObjects = this.showSceneObjects;
            m_Job.buildArgs.addRuntimeObjects = this.showRuntimeObjects;
            m_Job.buildArgs.addDestroyOnLoad = this.showDestroyOnLoadObjects;
            m_Job.buildArgs.addDontDestroyOnLoad = this.showDontDestroyOnLoadObjects;
            ScheduleJob(m_Job);
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

            if (m_Job != null && m_Job.state != AbstractThreadJob.State.Completed)
                window.SetBusy("Working...");
            else if (m_Job != null && m_Job.state == AbstractThreadJob.State.Completed)
                m_Job = null;
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
