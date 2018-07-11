using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace HeapExplorer
{
    public class GCHandlesView : HeapExplorerView
    {
        GCHandlesControl m_gcHandlesControl;
        HeSearchField m_objectsSearch;
        ConnectionsView m_connectionsView;
        PackedGCHandle? m_selected;
        RootPathView m_rootPathView;
        string m_editorPrefsKey = "";
        float m_splitterHorz = 0.33333f;
        float m_splitterVert = 0.32f;

        [InitializeOnLoadMethod]
        static void Register()
        {
            HeapExplorerWindow.Register<GCHandlesView>();
        }

        public override void Awake()
        {
            base.Awake();

            titleContent = new GUIContent("GC Handles", "");
            m_editorPrefsKey = "HeapExplorer.GCHandlesView";
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            m_connectionsView = CreateView<ConnectionsView>();
            m_connectionsView.editorPrefsKey = m_editorPrefsKey + ".ConnectionsView";

            m_rootPathView = CreateView<RootPathView>();
            m_rootPathView.editorPrefsKey = m_editorPrefsKey + ".RootPathView";

            m_gcHandlesControl = new GCHandlesControl(window, m_editorPrefsKey + ".GCHandlesControl", new TreeViewState());
            m_gcHandlesControl.SetTree(m_gcHandlesControl.BuildTree(snapshot));
            //m_gcHandlesControl.gotoCB += Goto;
            m_gcHandlesControl.onSelectionChange += OnListViewSelectionChange;

            m_objectsSearch = new HeSearchField(window);
            m_objectsSearch.downOrUpArrowKeyPressed += m_gcHandlesControl.SetFocusAndEnsureSelectedItem;
            m_gcHandlesControl.findPressed += m_objectsSearch.SetFocus;

            m_splitterHorz = EditorPrefs.GetFloat(m_editorPrefsKey + ".m_splitterHorz", m_splitterHorz);
            m_splitterVert = EditorPrefs.GetFloat(m_editorPrefsKey + ".m_splitterVert", m_splitterVert);
        }

        protected override void OnHide()
        {
            base.OnHide();

            m_gcHandlesControl.SaveLayout();

            EditorPrefs.SetFloat(m_editorPrefsKey + ".m_splitterHorz", m_splitterHorz);
            EditorPrefs.SetFloat(m_editorPrefsKey + ".m_splitterVert", m_splitterVert);
        }

        public override void RestoreCommand(GotoCommand command)
        {
            m_gcHandlesControl.Select(command.toGCHandle.packed);
        }

        public override int CanProcessCommand(GotoCommand command)
        {
            if (command.toGCHandle.isValid)
                return 10;

            return base.CanProcessCommand(command);
        }

        public override GotoCommand GetRestoreCommand()
        {
            if (m_selected.HasValue)
                return new GotoCommand(new RichGCHandle(snapshot, m_selected.Value.gcHandlesArrayIndex));

            return base.GetRestoreCommand();
        }

        // Called if the selection changed in the list that contains the managed objects overview.
        void OnListViewSelectionChange(PackedGCHandle? packedGCHandle)
        {
            m_selected = packedGCHandle;

            if (!packedGCHandle.HasValue)
            {
                m_rootPathView.Clear();
                m_connectionsView.Clear();
                return;
            }

            m_connectionsView.Inspect(packedGCHandle.Value);
            m_rootPathView.Inspect(m_selected.Value);
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
                            EditorGUILayout.LabelField(string.Format("{0} GCHandle(s)", snapshot.gcHandles.Length), EditorStyles.boldLabel);

                            if (m_objectsSearch.OnToolbarGUI())
                                m_gcHandlesControl.Search(m_objectsSearch.text);
                        }
                        GUILayout.Space(2);

                        m_gcHandlesControl.OnGUI();
                    }

                    HeEditorGUILayout.VerticalSplitter("m_splitterVert".GetHashCode(), ref m_splitterVert, 0.1f, 0.8f, window);

                    using (new EditorGUILayout.HorizontalScope(GUILayout.Height(window.position.height * m_splitterVert)))
                    {
                        m_connectionsView.OnGUI();
                    }
                }

                HeEditorGUILayout.HorizontalSplitter("m_splitterHorz".GetHashCode(), ref m_splitterHorz, 0.1f, 0.8f, window);

                using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel, GUILayout.Width(window.position.width * m_splitterHorz)))
                {
                    m_rootPathView.OnGUI();
                }
            }
        }
    }
}
