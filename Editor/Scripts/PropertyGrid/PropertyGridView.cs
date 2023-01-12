//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//

using HeapExplorer.Utilities;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using static HeapExplorer.Utilities.Option;

namespace HeapExplorer
{
    public class PropertyGridView : HeapExplorerView
    {
        PropertyGridControl m_PropertyGrid;
        AbstractDataVisualizer m_DataVisualizer;
        Vector2 m_DataVisualizerScrollPos;
        Option<RichManagedType> m_ManagedType;
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
                    var label = m_ManagedType.fold("Field(s)", managedType => managedType.name + " field(s)");
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

        public void Inspect(PackedManagedObject managedObject) {
            var richManagedObject = new RichManagedObject(snapshot, managedObject.managedObjectsArrayIndex);
            m_ManagedType = Some(richManagedObject.type);
            m_PropertyGrid.Inspect(snapshot, richManagedObject.packed);

            m_DataVisualizer = null;
            if (AbstractDataVisualizer.HasVisualizer(richManagedObject.type.name))
            {
                m_DataVisualizer = AbstractDataVisualizer.CreateVisualizer(richManagedObject.type.name);
                m_DataVisualizer.Initialize(snapshot, new MemoryReader(snapshot), richManagedObject.address, richManagedObject.type.packed);
            }

            m_HexView.Inspect(snapshot, managedObject.address, managedObject.size.getOrElse(0));
        }

        public void Inspect(RichManagedType managedType)
        {
            m_ManagedType = Some(managedType);
            m_PropertyGrid.InspectStaticType(snapshot, managedType.packed);
            m_HexView.Inspect(
                snapshot, 0, 
                new ArraySegment64<byte>(
                    managedType.packed.staticFieldBytes, 0, 
                    managedType.packed.staticFieldBytes.LongLength.ToULongClamped()
                )
            );

            m_DataVisualizer = null;
        }

        public void Clear()
        {
            m_ManagedType = None._;
            m_PropertyGrid.Clear();
            m_HexView.Clear();
            m_DataVisualizer = null;
        }
    }
}
