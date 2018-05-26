using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;

namespace HeapExplorer
{
    public class StaticFieldsView : HeapExplorerView
    {
        RichManagedType m_selected;
        StaticFieldsControl m_staticFieldsControl;
        HeSearchField m_SearchField;
        PropertyGridView m_propertyGridView;
        ConnectionsView m_connectionsView;
        string m_editorPrefsKey;
        float m_splitterHorz = 0.33333f;
        float m_splitterVert = 0.32f;

        public override void Awake()
        {
            base.Awake();

            title = new GUIContent("C# Static Fields");
            hasMainMenu = true;
            m_editorPrefsKey = "HeapExplorer.StaticFieldsView";
        }

        public override GenericMenu CreateMainMenu()
        {
            var menu = new GenericMenu();

            if (!m_selected.isValid)
                menu.AddDisabledItem(new GUIContent("Save selected field as file..."));
            else
                menu.AddItem(new GUIContent("Save selected field as file..."), false, OnSaveAsFile);

            return menu;
        }

        void OnSaveAsFile()
        {
            var filePath = EditorUtility.SaveFilePanel("Save", "", m_selected.name.Replace('.', '_'), "mem");
            if (string.IsNullOrEmpty(filePath))
                return;

            using (var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.OpenOrCreate))
            {
                var bytes = m_selected.packed.staticFieldBytes;
                fileStream.Write(bytes, 0, bytes.Length);
            }
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            m_connectionsView = CreateView<ConnectionsView>();
            m_connectionsView.gotoCB += Goto;
            m_connectionsView.editorPrefsKey = m_editorPrefsKey + ".m_connectionsView";
            m_connectionsView.showReferencedBy = false;

            // The list at the left that contains all native objects
            m_staticFieldsControl = new StaticFieldsControl(m_editorPrefsKey + ".m_staticFieldsControl", new TreeViewState());
            m_staticFieldsControl.SetTree(m_staticFieldsControl.BuildTree(m_snapshot));
            m_staticFieldsControl.gotoCB += Goto;
            m_staticFieldsControl.onTypeSelected += OnListViewTypeSelected;

            m_SearchField = new HeSearchField(window);
            m_SearchField.downOrUpArrowKeyPressed += m_staticFieldsControl.SetFocusAndEnsureSelectedItem;

            m_propertyGridView = CreateView<PropertyGridView>();
            m_propertyGridView.editorPrefsKey = m_editorPrefsKey + ".m_propertyGridView";

            m_splitterHorz = EditorPrefs.GetFloat(m_editorPrefsKey + ".m_splitterHorz", m_splitterHorz);
            m_splitterVert = EditorPrefs.GetFloat(m_editorPrefsKey + ".m_splitterVert", m_splitterVert);
        }

        protected override void OnHide()
        {
            base.OnHide();

            m_staticFieldsControl.SaveLayout();

            EditorPrefs.SetFloat(m_editorPrefsKey + ".m_splitterHorz", m_splitterHorz);
            EditorPrefs.SetFloat(m_editorPrefsKey + ".m_splitterVert", m_splitterVert);
        }

        public override GotoCommand GetRestoreCommand()
        {
            if (m_selected.isValid)
                return new GotoCommand(m_selected) { toKind = GotoCommand.EKind.StaticClass };

            return null;
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
                            var text = string.Format("{0} static fields in {1} types", m_snapshot.managedStaticFields.Length, m_snapshot.managedStaticTypes.Length);
                            window.SetStatusbarString(text);
                            //EditorGUILayout.LabelField(string.Format("{0} static fields in {1} types", m_snapshot.managedStaticFields.Length, m_snapshot.managedStaticTypes.Length), EditorStyles.boldLabel);
                            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                            if (m_SearchField.OnToolbarGUI())
                                m_staticFieldsControl.Search(m_SearchField.text);
                        }
                        GUILayout.Space(2);

                        m_staticFieldsControl.OnGUI();
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
                    m_propertyGridView.OnGUI();
                }
            }
        }

        public void Select(PackedManagedType packed)
        {
            m_staticFieldsControl.Select(packed);
        }

        public void Select(PackedManagedStaticField packed)
        {
            if (packed.managedTypesArrayIndex == -1)
                return;

            var type = m_snapshot.managedTypes[packed.managedTypesArrayIndex];
            m_staticFieldsControl.Select(type);
        }
        
        void OnListViewTypeSelected(PackedManagedType? type)
        {
            if (!type.HasValue)
            {
                m_selected = RichManagedType.invalid;
                m_connectionsView.Clear();
                m_propertyGridView.Clear();
                return;
            }

            m_selected = new RichManagedType(m_snapshot, type.Value.managedTypesArrayIndex);
            var staticClass = m_selected.packed;
            var staticFields = new List<PackedManagedStaticField>();

            // Find all static fields of selected type
            foreach (var sf in m_snapshot.managedStaticFields)
            {
                if (sf.managedTypesArrayIndex == staticClass.managedTypesArrayIndex)
                    staticFields.Add(sf);
            }
            m_connectionsView.Inspect(staticFields.ToArray());

            m_propertyGridView.Inspect(m_selected);
        }
    }
}
