//
// Heap Explorer for Unity. Copyright (c) 2019 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://bitbucket.org/pschraut/unityheapexplorer/
//
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace HeapExplorer
{
    public class ManagedTypesView : HeapExplorerView
    {
        ManagedTypesControl m_TypesControl;
        HeSearchField m_TypesSearchField;
        PackedManagedType? m_Selected;
        float m_SplitterHorz = 0.33333f;
        float m_SplitterVert = 0.32f;

        [InitializeOnLoadMethod]
        static void Register()
        {
            HeapExplorerWindow.Register<ManagedTypesView>();
        }

        public override void Awake()
        {
            base.Awake();

            titleContent = new GUIContent("C# Types", "");
            viewMenuOrder = 345;
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            m_TypesControl = new ManagedTypesControl(window, GetPrefsKey(() => m_TypesControl), new TreeViewState());
            m_TypesControl.SetTree(m_TypesControl.BuildTree(snapshot));
            m_TypesControl.onSelectionChange += OnListViewSelectionChange;

            m_TypesSearchField = new HeSearchField(window);
            m_TypesSearchField.downOrUpArrowKeyPressed += m_TypesControl.SetFocusAndEnsureSelectedItem;
            m_TypesControl.findPressed += m_TypesSearchField.SetFocus;

            m_SplitterHorz = EditorPrefs.GetFloat(GetPrefsKey(() => m_SplitterHorz), m_SplitterHorz);
            m_SplitterVert = EditorPrefs.GetFloat(GetPrefsKey(() => m_SplitterVert), m_SplitterVert);
        }

        protected override void OnHide()
        {
            base.OnHide();

            m_TypesControl.SaveLayout();

            EditorPrefs.SetFloat(GetPrefsKey(() => m_SplitterHorz), m_SplitterHorz);
            EditorPrefs.SetFloat(GetPrefsKey(() => m_SplitterVert), m_SplitterVert);
        }

        public override void RestoreCommand(GotoCommand command)
        {
            //m_TypesControl.Select(command.toGCHandle.packed);
        }

        //public override int CanProcessCommand(GotoCommand command)
        //{
        //    if (command.toGCHandle.isValid)
        //        return 10;

        //    return base.CanProcessCommand(command);
        //}

        //public override GotoCommand GetRestoreCommand()
        //{
        //    if (m_Selected.HasValue)
        //        return new GotoCommand(new RichGCHandle(snapshot, m_Selected.Value.gcHandlesArrayIndex));

        //    return base.GetRestoreCommand();
        //}

        // Called if the selection changed in the list that contains the managed objects overview.
        void OnListViewSelectionChange(PackedManagedType? type)
        {
            m_Selected = type;

            if (!type.HasValue)
            {
                return;
            }
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
                            EditorGUILayout.LabelField(string.Format("{0} C# Type(s)", snapshot.managedTypes.Length), EditorStyles.boldLabel);

                            if (m_TypesSearchField.OnToolbarGUI())
                                m_TypesControl.Search(m_TypesSearchField.text);
                        }
                        GUILayout.Space(2);

                        m_TypesControl.OnGUI();
                    }

                    //m_SplitterVert = HeEditorGUILayout.VerticalSplitter("m_splitterVert".GetHashCode(), m_SplitterVert, 0.1f, 0.8f, window);

                    //using (new EditorGUILayout.HorizontalScope(GUILayout.Height(window.position.height * m_SplitterVert)))
                    //{
                    //    m_ConnectionsView.OnGUI();
                    //}
                }

                //m_SplitterHorz = HeEditorGUILayout.HorizontalSplitter("m_splitterHorz".GetHashCode(), m_SplitterHorz, 0.1f, 0.8f, window);

                //using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel, GUILayout.Width(window.position.width * m_SplitterHorz)))
                //{
                //    m_RootPathView.OnGUI();
                //}
            }
        }
    }
}
