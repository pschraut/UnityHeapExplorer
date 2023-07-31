﻿//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//
using System.Collections.Generic;
using HeapExplorer.Utilities;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;
using static HeapExplorer.Utilities.Option;

namespace HeapExplorer
{
    public class StaticFieldsView : HeapExplorerView
    {
        Option<RichManagedType> m_Selected;
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

                if (m_Selected.valueOut(out var selected))
                    menu.AddItem(new GUIContent("Save selected field as file..."), false, () => OnSaveAsFile(selected));
                else
                    menu.AddDisabledItem(new GUIContent("Save selected field as file..."));

                menu.DropDown(m_ToolbarButtonRect);
            }

            if (Event.current.type == EventType.Repaint)
                m_ToolbarButtonRect = GUILayoutUtility.GetLastRect();
        }

        void OnSaveAsFile(RichManagedType selected)
        {
            var filePath = EditorUtility.SaveFilePanel("Save", "", selected.name.Replace('.', '_'), "mem");
            if (string.IsNullOrEmpty(filePath))
                return;

            using (var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.OpenOrCreate))
            {
                var bytes = selected.packed.staticFieldBytes;
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

        public override GotoCommand GetRestoreCommand() => 
            m_Selected.valueOut(out var selected) ? new GotoCommand(selected) : base.GetRestoreCommand();

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
                            var text =
                                $"{snapshot.managedStaticFields.Length} static fields in {snapshot.managedStaticTypes.Length} types";
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
            if (command.toStaticField.isSome || command.toManagedType.isSome)
                return 10;

            return base.CanProcessCommand(command);
        }

        public override void RestoreCommand(GotoCommand command)
        {
            {if (command.toStaticField.valueOut(out var staticField)) {
                m_StaticFieldsControl.Select(staticField.classType.packed);
                return;
            }}

            {if (command.toManagedType.valueOut(out var managedType)) {
                m_StaticFieldsControl.Select(managedType.packed);
            }}
        }

        void OnListViewTypeSelected(Option<PackedManagedType> maybeType)
        {
            if (!maybeType.valueOut(out var type))
            {
                m_Selected = None._;
                m_ConnectionsView.Clear();
                m_PropertyGridView.Clear();
                return;
            }

            var selected = new RichManagedType(snapshot, type.managedTypesArrayIndex);
            m_Selected = Some(selected);
            var staticClass = selected.packed;
            var staticFields = new List<PackedManagedStaticField>();

            // Find all static fields of selected type
            foreach (var sf in snapshot.managedStaticFields)
            {
                if (sf.managedTypesArrayIndex == staticClass.managedTypesArrayIndex)
                    staticFields.Add(sf);
            }
            m_ConnectionsView.Inspect(staticFields.ToArray());

            m_PropertyGridView.Inspect(selected);
        }
    }
}
