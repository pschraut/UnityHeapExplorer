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
    public class StaticFieldsView : HeapExplorerView
    {
        RichManagedType m_Selected;
        StaticFieldsControl m_StaticFieldsControl;
        HeSearchField m_SearchField;
        PropertyGridView m_PropertyGridView;
        ConnectionsView m_ConnectionsView;
        float m_SplitterHorz = 0.33333f;
        float m_SplitterVert = 0.32f;
        Rect m_ToolbarButtonRect;

        [InitializeOnLoadMethod]
        static void Register()
        {
            HeapExplorerWindow.Register<StaticFieldsView>();
        }

        public override void Awake()
        {
            base.Awake();

            titleContent = new GUIContent("C# Static Fields");
            viewMenuOrder = 350;
        }

        public override void OnToolbarGUI()
        {
            base.OnToolbarGUI();

            if (GUILayout.Button(new GUIContent("Tools"), EditorStyles.toolbarDropDown, GUILayout.Width(70)))
            {
                var menu = new GenericMenu();

                if (!m_Selected.isValid)
                    menu.AddDisabledItem(new GUIContent("Save selected field as file..."));
                else
                    menu.AddItem(new GUIContent("Save selected field as file..."), false, OnSaveAsFile);

                menu.DropDown(m_ToolbarButtonRect);
            }

            if (Event.current.type == EventType.Repaint)
                m_ToolbarButtonRect = GUILayoutUtility.GetLastRect();
        }

        void OnSaveAsFile()
        {
            var filePath = EditorUtility.SaveFilePanel("Save", "", m_Selected.name.Replace('.', '_'), "mem");
            if (string.IsNullOrEmpty(filePath))
                return;

            using (var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.OpenOrCreate))
            {
                var bytes = m_Selected.packed.staticFieldBytes;
                fileStream.Write(bytes, 0, bytes.Length);
            }
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            m_ConnectionsView = CreateView<ConnectionsView>();
            m_ConnectionsView.editorPrefsKey = GetPrefsKey(() => m_ConnectionsView);
            m_ConnectionsView.showReferencedBy = false;

            // The list at the left that contains all native objects
            m_StaticFieldsControl = new StaticFieldsControl(window, GetPrefsKey(() => m_StaticFieldsControl), new TreeViewState());
            m_StaticFieldsControl.SetTree(m_StaticFieldsControl.BuildTree(snapshot));
            m_StaticFieldsControl.onTypeSelected += OnListViewTypeSelected;

            m_SearchField = new HeSearchField(window);
            m_SearchField.downOrUpArrowKeyPressed += m_StaticFieldsControl.SetFocusAndEnsureSelectedItem;

            m_PropertyGridView = CreateView<PropertyGridView>();
            m_PropertyGridView.editorPrefsKey = GetPrefsKey(() => m_PropertyGridView);

            m_SplitterHorz = EditorPrefs.GetFloat(GetPrefsKey(() => m_SplitterHorz), m_SplitterHorz);
            m_SplitterVert = EditorPrefs.GetFloat(GetPrefsKey(() => m_SplitterVert), m_SplitterVert);
        }

        protected override void OnHide()
        {
            base.OnHide();

            m_StaticFieldsControl.SaveLayout();

            EditorPrefs.SetFloat(GetPrefsKey(() => m_SplitterHorz), m_SplitterHorz);
            EditorPrefs.SetFloat(GetPrefsKey(() => m_SplitterVert), m_SplitterVert);
        }

        public override GotoCommand GetRestoreCommand()
        {
            if (m_Selected.isValid)
                return new GotoCommand(m_Selected);

            return base.GetRestoreCommand();
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
                            var text = string.Format("{0} static fields in {1} types", snapshot.managedStaticFields.Length, snapshot.managedStaticTypes.Length);
                            window.SetStatusbarString(text);
                            EditorGUILayout.LabelField(titleContent, EditorStyles.boldLabel);
                            if (m_SearchField.OnToolbarGUI())
                                m_StaticFieldsControl.Search(m_SearchField.text);
                        }
                        GUILayout.Space(2);

                        m_StaticFieldsControl.OnGUI();
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
                    m_PropertyGridView.OnGUI();
                }
            }
        }

        public override int CanProcessCommand(GotoCommand command)
        {
            if (command.toStaticField.isValid || command.toManagedType.isValid)
                return 10;

            return base.CanProcessCommand(command);
        }

        public override void RestoreCommand(GotoCommand command)
        {
            if (command.toStaticField.isValid)
            {
                m_StaticFieldsControl.Select(command.toStaticField.classType.packed);
                return;
            }

            if (command.toManagedType.isValid)
            {
                m_StaticFieldsControl.Select(command.toManagedType.packed);
            }
        }
        
        void OnListViewTypeSelected(PackedManagedType? type)
        {
            if (!type.HasValue)
            {
                m_Selected = RichManagedType.invalid;
                m_ConnectionsView.Clear();
                m_PropertyGridView.Clear();
                return;
            }

            m_Selected = new RichManagedType(snapshot, type.Value.managedTypesArrayIndex);
            var staticClass = m_Selected.packed;
            var staticFields = new List<PackedManagedStaticField>();

            // Find all static fields of selected type
            foreach (var sf in snapshot.managedStaticFields)
            {
                if (sf.managedTypesArrayIndex == staticClass.managedTypesArrayIndex)
                    staticFields.Add(sf);
            }
            m_ConnectionsView.Inspect(staticFields.ToArray());

            m_PropertyGridView.Inspect(m_Selected);
        }
    }
}
