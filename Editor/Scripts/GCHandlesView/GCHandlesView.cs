//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//
using System.Collections;
using System.Collections.Generic;
using HeapExplorer.Utilities;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace HeapExplorer
{
    public class GCHandlesView : HeapExplorerView
    {
        GCHandlesControl m_HandlesControl;
        HeSearchField m_HandlesSearchField;
        ConnectionsView m_ConnectionsView;
        Option<PackedGCHandle> m_Selected;
        RootPathView m_RootPathView;
        float m_SplitterHorz = 0.33333f;
        float m_SplitterVert = 0.32f;

        [InitializeOnLoadMethod]
        static void Register()
        {
            HeapExplorerWindow.Register<GCHandlesView>();
        }

        public override void Awake()
        {
            base.Awake();

            titleContent = new GUIContent("GC Handles", "");
            viewMenuOrder = 750;
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            m_ConnectionsView = CreateView<ConnectionsView>();
            m_ConnectionsView.editorPrefsKey = GetPrefsKey(() => m_ConnectionsView);

            m_RootPathView = CreateView<RootPathView>();
            m_RootPathView.editorPrefsKey = GetPrefsKey(() => m_RootPathView);

            m_HandlesControl = new GCHandlesControl(window, GetPrefsKey(() => m_HandlesControl), new TreeViewState());
            m_HandlesControl.SetTree(m_HandlesControl.BuildTree(snapshot));
            m_HandlesControl.onSelectionChange += OnListViewSelectionChange;

            m_HandlesSearchField = new HeSearchField(window);
            m_HandlesSearchField.downOrUpArrowKeyPressed += m_HandlesControl.SetFocusAndEnsureSelectedItem;
            m_HandlesControl.findPressed += m_HandlesSearchField.SetFocus;

            m_SplitterHorz = EditorPrefs.GetFloat(GetPrefsKey(() => m_SplitterHorz), m_SplitterHorz);
            m_SplitterVert = EditorPrefs.GetFloat(GetPrefsKey(() => m_SplitterVert), m_SplitterVert);
        }

        protected override void OnHide()
        {
            base.OnHide();

            m_HandlesControl.SaveLayout();

            EditorPrefs.SetFloat(GetPrefsKey(() => m_SplitterHorz), m_SplitterHorz);
            EditorPrefs.SetFloat(GetPrefsKey(() => m_SplitterVert), m_SplitterVert);
        }

        public override void RestoreCommand(GotoCommand command)
        {
            if (!command.toGCHandle.valueOut(out var gcHandle)) {
                Debug.LogError($"{nameof(RestoreCommand)}({command}) failed: no gc handle");
                return;
            }
            m_HandlesControl.Select(gcHandle.packed);
        }

        public override int CanProcessCommand(GotoCommand command)
        {
            if (command.toGCHandle.isSome)
                return 10;

            return base.CanProcessCommand(command);
        }

        public override GotoCommand GetRestoreCommand()
        {
            if (m_Selected.valueOut(out var gcHandle))
                return new GotoCommand(new RichGCHandle(snapshot, gcHandle.gcHandlesArrayIndex));

            return base.GetRestoreCommand();
        }

        // Called if the selection changed in the list that contains the managed objects overview.
        void OnListViewSelectionChange(Option<PackedGCHandle> packedGCHandle)
        {
            m_Selected = packedGCHandle;

            if (!packedGCHandle.valueOut(out var gcHandle))
            {
                m_RootPathView.Clear();
                m_ConnectionsView.Clear();
                return;
            }

            m_ConnectionsView.Inspect(gcHandle);
            m_RootPathView.Inspect(gcHandle);
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
                            EditorGUILayout.LabelField($"{snapshot.gcHandles.Length} GCHandle(s)", EditorStyles.boldLabel);

                            if (m_HandlesSearchField.OnToolbarGUI())
                                m_HandlesControl.Search(m_HandlesSearchField.text);
                        }
                        GUILayout.Space(2);

                        m_HandlesControl.OnGUI();
                    }

                    m_SplitterVert = HeEditorGUILayout.VerticalSplitter("m_splitterVert".GetHashCode(), m_SplitterVert, 0.1f, 0.8f, window);

                    using (new EditorGUILayout.HorizontalScope(GUILayout.Height(window.position.height * m_SplitterVert)))
                    {
                        m_ConnectionsView.OnGUI();
                    }
                }

                m_SplitterHorz = HeEditorGUILayout.HorizontalSplitter("m_splitterHorz".GetHashCode(), m_SplitterHorz, 0.1f, 0.8f, window);

                using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel, GUILayout.Width(window.position.width * m_SplitterHorz)))
                {
                    m_RootPathView.OnGUI();
                }
            }
        }
    }
}
