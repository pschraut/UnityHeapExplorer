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
    public class PropertyGridView : HeapExplorerView
    {
        PropertyGridControl m_PropertyGrid;
        AbstractDataVisualizer m_DataVisualizer;
        Vector2 m_DataVisualizerScrollPos;
        RichManagedObject m_ManagedObject;
        RichManagedType m_ManagedType;
        bool m_ShowAsHex;
        HexView m_HexView;

        public override void Awake()
        {
            base.Awake();

            titleContent = new GUIContent("C# Object", "");
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            m_HexView = CreateView<HexView>();
            m_ShowAsHex = EditorPrefs.GetBool(GetPrefsKey(() => m_ShowAsHex), false);

            m_PropertyGrid = new PropertyGridControl(window, GetPrefsKey(() => m_PropertyGrid), new TreeViewState());
        }

        protected override void OnHide()
        {
            base.OnHide();

            EditorPrefs.SetBool(GetPrefsKey(() => m_ShowAsHex), m_ShowAsHex);
        }

        public override void OnGUI()
        {
            base.OnGUI();

            using (new EditorGUILayout.VerticalScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var label = "Field(s)";
                    if (m_ManagedType.isValid)
                        label = m_ManagedType.name + " field(s)";
                    EditorGUILayout.LabelField(label, EditorStyles.boldLabel, GUILayout.ExpandWidth(true));

                    m_ShowAsHex = GUILayout.Toggle(m_ShowAsHex, new GUIContent(HeEditorStyles.eyeImage, "Show Memory"), EditorStyles.miniButton, GUILayout.Width(30), GUILayout.Height(17));
                }

                if (m_ShowAsHex != m_HexView.isVisible)
                {
                    if (m_ShowAsHex)
                        m_HexView.Show(snapshot);
                    else
                        m_HexView.Hide();
                }
                
                if (m_ShowAsHex)
                    m_HexView.OnGUI();
                else
                    m_PropertyGrid.OnGUI();
            }
        }

        public void Inspect(PackedManagedObject managedObject)
        {
            m_ManagedObject = new RichManagedObject(snapshot, managedObject.managedObjectsArrayIndex);
            m_ManagedType = m_ManagedObject.type;
            m_PropertyGrid.Inspect(snapshot, m_ManagedObject.packed);

            m_DataVisualizer = null;
            if (AbstractDataVisualizer.HasVisualizer(m_ManagedObject.type.name))
            {
                m_DataVisualizer = AbstractDataVisualizer.CreateVisualizer(m_ManagedObject.type.name);
                m_DataVisualizer.Initialize(snapshot, new MemoryReader(snapshot), m_ManagedObject.address, m_ManagedObject.type.packed);
            }

            m_HexView.Inspect(snapshot, managedObject.address, (ulong)managedObject.size);
        }

        public void Inspect(RichManagedType managedType)
        {
            m_ManagedObject = RichManagedObject.invalid;
            m_ManagedType = managedType;
            m_PropertyGrid.InspectStaticType(snapshot, m_ManagedType.packed);
            m_HexView.Inspect(snapshot, 0, new ArraySegment64<byte>(managedType.packed.staticFieldBytes, 0, (ulong)managedType.packed.staticFieldBytes.LongLength));

            m_DataVisualizer = null;
        }

        public void Clear()
        {
            m_ManagedObject = RichManagedObject.invalid;
            m_ManagedType = RichManagedType.invalid;
            m_PropertyGrid.Clear();
            m_HexView.Clear();
            m_DataVisualizer = null;
        }
    }
}
