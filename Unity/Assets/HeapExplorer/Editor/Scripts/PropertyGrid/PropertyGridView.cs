using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace HeapExplorer
{
    public class PropertyGridView : HeapExplorerView
    {
        PropertyGridControl m_propertyGrid;
        AbstractDataVisualizer m_dataVisualizer;
        Vector2 m_dataVisualizerScrollPos;
        RichManagedObject m_managedObject;
        RichManagedType m_managedType;
        bool m_showAsHex;
        HexView m_hexView;

        public string editorPrefsKey
        {
            get;
            set;
        }

        public override void Awake()
        {
            base.Awake();

            titleContent = new GUIContent("C# Objects", "");
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            m_hexView = CreateView<HexView>();
            m_showAsHex = EditorPrefs.GetBool(editorPrefsKey + "m_showAsHex", false);

            m_propertyGrid = new PropertyGridControl(window, editorPrefsKey + "m_propertyGrid", new TreeViewState());
            //m_propertyGrid.gotoCB += Goto;
        }

        protected override void OnHide()
        {
            base.OnHide();

            EditorPrefs.SetBool(editorPrefsKey + "m_showAsHex", m_showAsHex);
        }

        public override void OnGUI()
        {
            base.OnGUI();

            using (new EditorGUILayout.VerticalScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var label = "Field(s)";
                    if (m_managedType.isValid)
                        label = m_managedType.name + " field(s)";
                    EditorGUILayout.LabelField(label, EditorStyles.boldLabel, GUILayout.ExpandWidth(true));

                    //GUILayout.FlexibleSpace();

                    m_showAsHex = GUILayout.Toggle(m_showAsHex, new GUIContent(HeEditorStyles.eyeImage, "Show Memory"), EditorStyles.miniButton, GUILayout.Width(30), GUILayout.Height(17));
                }

                if (m_showAsHex != m_hexView.isVisible)
                {
                    if (m_showAsHex)
                        m_hexView.Show(snapshot);
                    else
                        m_hexView.Hide();
                }
                
                if (m_showAsHex)
                    m_hexView.OnGUI();
                else
                    m_propertyGrid.OnGUI();
            }
        }

        public void Inspect(PackedManagedObject managedObject)
        {
            m_managedObject = new RichManagedObject(snapshot, managedObject.managedObjectsArrayIndex);
            m_managedType = m_managedObject.type;
            m_propertyGrid.Inspect(snapshot, m_managedObject.packed);

            m_dataVisualizer = null;
            if (AbstractDataVisualizer.HasVisualizer(m_managedObject.type.name))
            {
                m_dataVisualizer = AbstractDataVisualizer.CreateVisualizer(m_managedObject.type.name);
                m_dataVisualizer.Initialize(snapshot, new MemoryReader(snapshot), m_managedObject.address, m_managedObject.type.packed);
            }

            m_hexView.Inspect(snapshot, managedObject.address, (ulong)managedObject.size);
        }

        public void Inspect(RichManagedType managedType)
        {
            m_managedObject = RichManagedObject.invalid;
            m_managedType = managedType;
            m_propertyGrid.InspectStaticType(snapshot, m_managedType.packed);
            m_hexView.Inspect(snapshot, 0, new ArraySegment64<byte>(managedType.packed.staticFieldBytes, 0, (ulong)managedType.packed.staticFieldBytes.LongLength));

            m_dataVisualizer = null;
        }

        public void Clear()
        {
            m_managedObject = RichManagedObject.invalid;
            m_managedType = RichManagedType.invalid;
            m_propertyGrid.Clear();
            m_hexView.Clear();
            m_dataVisualizer = null;
        }
    }
}
