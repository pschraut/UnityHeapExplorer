//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using System;

namespace HeapExplorer
{
    public class ManagedEmptyShellObjectsView : AbstractManagedObjectsView
    {
        [InitializeOnLoadMethod]
        static void Register()
        {
            HeapExplorerWindow.Register<ManagedEmptyShellObjectsView>();
        }

        public override void Awake()
        {
            base.Awake();

            titleContent = new GUIContent("C# Empty Shell Objects", "");
            viewMenuOrder = 258;
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            var job = new Job();
            job.snapshot = snapshot;
            job.control = (ManagedEmptyShellObjectsControl)m_ObjectsControl;
            ScheduleJob(job);
        }

        protected override AbstractManagedObjectsControl CreateObjectsTreeView(string editorPrefsKey, TreeViewState state)
        {
            return new ManagedEmptyShellObjectsControl(window, editorPrefsKey, state);
        }

        protected override void OnDrawHeader()
        {
            var text = $"{m_ObjectsControl.managedObjectsCount} empty shell object(s)";
            window.SetStatusbarString(text);
            EditorGUILayout.LabelField(titleContent, EditorStyles.boldLabel);
        }

        public override void OnGUI()
        {
            base.OnGUI();

            var control = (ManagedEmptyShellObjectsControl)m_ObjectsControl;
            if (control.progress.value < 1)
            {
                window.SetBusy($"Analyzing Managed Objects, {control.progress.value * 100:F0}% done");
            }
        }

        class Job : AbstractThreadJob
        {
            public ManagedEmptyShellObjectsControl control;
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
