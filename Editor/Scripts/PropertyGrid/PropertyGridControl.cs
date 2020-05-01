//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;

namespace HeapExplorer
{
    public class PropertyGridControl : AbstractTreeView
    {
        PackedMemorySnapshot m_Snapshot;
        AbstractDataVisualizer m_DataVisualizer;
        Vector2 m_DataVisualizerScrollPos;

        float splitterDataVisualizer
        {
            get
            {
                var key = m_EditorPrefsKey + ".m_splitterHorzPropertyGrid";
                return EditorPrefs.GetFloat(key, 0.15f);
            }
            set
            {
                var key = m_EditorPrefsKey + ".m_splitterHorzPropertyGrid";
                EditorPrefs.SetFloat(key, value);
            }
        }

        public PropertyGridControl(EditorWindow window, string editorPrefsKey, TreeViewState state)
            : base(window as HeapExplorerWindow, editorPrefsKey, state, new MultiColumnHeader(
                new MultiColumnHeaderState(new[]
                {
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Name"), width = 200, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Value"), width = 200, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Type"), width = 200, autoResize = true }
                } )))
        {
            multiColumnHeader.canSort = false;
            Reload();
        }

        protected override bool CanChangeExpandedState(TreeViewItem itm)
        {
            var item = itm as PropertyGridItem;
            if (item != null)
                return item.isExpandable;

            return itm.hasChildren;
        }

        protected override void OnExpandedChanged(TreeViewItem itm, bool expanded)
        {
            base.OnExpandedChanged(itm, expanded);

            var item = itm as PropertyGridItem;
            if (item != null)
                item.BuildChildren(AddType);
        }

        public void Inspect(PackedMemorySnapshot snapshot, PackedManagedObject managedObject)
        {
            m_Snapshot = snapshot;
            var m_type = snapshot.managedTypes[managedObject.managedTypesArrayIndex];
            var m_address = managedObject.address;
            if (m_type.isValueType)
                m_address -= (ulong)m_Snapshot.virtualMachineInformation.objectHeaderSize;

            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
            if (m_Snapshot == null)
            {
                root.AddChild(new TreeViewItem { id = 1, depth = -1, displayName = "" });
                return;
            }

            var args = new PropertyGridItem.BuildChildrenArgs();
            args.parent = root;
            args.type = m_type;
            args.address = m_address;
            args.memoryReader = new MemoryReader(snapshot);

            AddType(args);

            // add at least one item to the tree, otherwise it outputs an error
            if (!root.hasChildren)
                root.AddChild(new TreeViewItem(1, 0, ""));

            SetTree(root);
            TryCreateDataVisualizer(args.memoryReader, args.type, args.address, false);
        }

        public void InspectStaticType(PackedMemorySnapshot snapshot, PackedManagedType managedType)
        {
            m_Snapshot = snapshot;
            var m_type = managedType;
            var m_address = 0ul;

            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
            if (m_Snapshot == null)
            {
                root.AddChild(new TreeViewItem { id = 1, depth = -1, displayName = "" });
                return;
            }

            var args = new PropertyGridItem.BuildChildrenArgs();
            args.parent = root;
            args.type = m_type;
            args.address = m_address;
            args.memoryReader = new StaticMemoryReader(snapshot, managedType.staticFieldBytes);

            AddType(args);

            // add at least one item to the tree, otherwise it outputs an error
            if (!root.hasChildren)
                root.AddChild(new TreeViewItem(1, 0, ""));

            SetTree(root);
            TryCreateDataVisualizer(args.memoryReader, args.type, args.address, false);
        }

        public void Clear()
        {
            m_Snapshot = null;
            SetTree(null);
            m_DataVisualizer = null;
            m_DataVisualizerScrollPos = new Vector2();
        }

        public override void OnGUI()
        {
            base.OnGUI();

            DrawDataVisualizer();
        }

        protected override void OnSelectionChanged(TreeViewItem selectedItem)
        {
            base.OnSelectionChanged(selectedItem);

            m_DataVisualizer = null;
            m_DataVisualizerScrollPos = new Vector2();
            var item = selectedItem as PropertyGridItem;
            if (item != null)
                TryCreateDataVisualizer(item.myMemoryReader, item.type, item.address, true);
        }

        void TryCreateDataVisualizer(AbstractMemoryReader reader, PackedManagedType type, ulong address, bool resolveAddress)
        {
            m_DataVisualizer = null;
            m_DataVisualizerScrollPos = new Vector2();

            //if (AbstractDataVisualizer.HasVisualizer(type.name))
            {
                if (type.isPointer && resolveAddress)
                    address = reader.ReadPointer(address);

                m_DataVisualizer = AbstractDataVisualizer.CreateVisualizer(type.name);
                m_DataVisualizer.Initialize(m_Snapshot, reader, address, type);
            }
        }

        void DrawDataVisualizer()
        {
            if (m_DataVisualizer == null)
                return;

            GUILayout.Space(2);
            splitterDataVisualizer = HeEditorGUILayout.VerticalSplitter("splitterDataVisualizer".GetHashCode(), splitterDataVisualizer, 0.1f, 0.6f, window);
            GUILayout.Space(2);

            using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel, GUILayout.Height(window.position.height * splitterDataVisualizer)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    // We draw a dataVisualizer always, but don't display the content of the fallback visualizer.
                    // This is to keep the UI free from hiding/showing the visualizer panel and keep it more stable.
                    // TODO: This is actually a HACK until I fixed the default preview
                    if (!m_DataVisualizer.isFallback)
                        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

                    if (m_DataVisualizer.hasMenu)
                    {
                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button(new GUIContent("", "Open menu"), HeEditorStyles.paneOptions, GUILayout.Width(20)))
                        {
                            m_DataVisualizer.ShowMenu();
                        }
                    }
                }

                GUILayout.Space(4);

                using (var scrollView = new EditorGUILayout.ScrollViewScope(m_DataVisualizerScrollPos))
                {
                    m_DataVisualizerScrollPos = scrollView.scrollPosition;

                    m_DataVisualizer.GUI();
                }
            }
        }

        void AddType(PropertyGridItem.BuildChildrenArgs args)
        {
            var target = args.parent;
            var type = args.type;
            var address = args.address;
            var reader = args.memoryReader;
            var addStatic = reader is StaticMemoryReader;
            var addInstance = !addStatic;

            if (target.depth > 64)
            {
                Debug.LogFormat("recursive reference found? type: {0}", type.name);
                return;
            }

            // Add the base class, if any, to the tree.
            if (type.isDerivedReferenceType && !type.isArray)
            {
                var baseType = m_Snapshot.managedTypes[type.baseOrElementTypeIndex];
                var isSystemObject = baseType.managedTypesArrayIndex == m_Snapshot.coreTypes.systemObject;
                if (!isSystemObject && PackedManagedTypeUtility.HasTypeOrBaseAnyField(m_Snapshot, baseType, !addStatic, addStatic))
                {
                    var item = new BaseClassPropertyGridItem(this, m_Snapshot, address, reader)
                    {
                        depth = target.depth + 1,
                        type = baseType
                    };
                    item.Initialize();

                    target.AddChild(item);
                }
            }

            // Array
            if (type.isArray)
            {
                // Normally the baseOrElementTypeIndex of an array represents the element type.
                // If inspecting static generic fields though, there is no element type available for '_EmptyArray'.
                // class ArrayList<T>
                // {
                //   static readonly T[] _EmptyArray = new T[0];
                // }
                if (type.baseOrElementTypeIndex == -1)
                    return;

                var pointer = address;
                var item = new ArrayPropertyGridItem(this, m_Snapshot, pointer, reader)
                {
                    depth = target.depth + 1,
                    type = type,
                    displayName = type.name,
                };
                item.Initialize();

                target.AddChild(item);
                return;
            }



            for (var n = 0; n < type.fields.Length; ++n)
            {
                if (type.fields[n].isStatic && !addStatic)
                    continue;

                if (!type.fields[n].isStatic && !addInstance)
                    continue;

                var fieldType = m_Snapshot.managedTypes[type.fields[n].managedTypesArrayIndex];

                // Array
                if (fieldType.isArray)
                {
                    if (fieldType.baseOrElementTypeIndex == -1)
                        continue;

                    var pointer = reader.ReadPointer(address + (ulong)type.fields[n].offset);
                    var item = new ArrayPropertyGridItem(this, m_Snapshot, pointer, new MemoryReader(m_Snapshot))
                    {
                        depth = target.depth + 1,
                        type = fieldType,
                        displayName = type.fields[n].name
                    };
                    item.Initialize();

                    target.AddChild(item);
                    continue;
                }

                // Primitive types and types derived from System.Enum
                if (fieldType.isValueType && (fieldType.isPrimitive || m_Snapshot.IsEnum(fieldType)))
                {
                    var item = new PrimitiveTypePropertyGridItem(this, m_Snapshot, address + (ulong)type.fields[n].offset, reader)
                    {
                        depth = target.depth + 1,
                        field = type.fields[n]
                    };
                    item.Initialize();

                    target.AddChild(item);
                    continue;
                }

                // Value types
                if (fieldType.isValueType)
                {
                    var item = new ValueTypePropertyGridItem(this, m_Snapshot, address + (ulong)type.fields[n].offset, reader)
                    {
                        depth = target.depth + 1,
                        field = type.fields[n]
                    };
                    item.Initialize();

                    target.AddChild(item);
                    continue;
                }

                // Reference types
                //if (fieldType.isPointer)
                {
                    var item = new ReferenceTypePropertyGridItem(this, m_Snapshot, address + (ulong)type.fields[n].offset, reader)
                    {
                        depth = target.depth + 1,
                        field = type.fields[n]
                    };
                    item.Initialize();

                    target.AddChild(item);
                    continue;
                }
            }
        }

        protected override int OnSortItem(TreeViewItem x, TreeViewItem y)
        {
            return 0;
        }
    }




#if false
    public class ManagedObjectInspectorWindow : EditorWindow
    {
        PropertyGridControl m_PropertyGrid;
        PackedMemorySnapshot m_Snapshot;
        string m_ErrorString = "";
        PackedManagedObject m_ManagedObject;

        private void OnEnable()
        {
            this.minSize = new Vector2(200, 100);
        }

        static public void Inspect(PackedMemorySnapshot snapshot, PackedManagedObject managedObject)
        {
            var wnd = MemoryWindow.CreateInstance<ManagedObjectInspectorWindow>();
            wnd.InspectInternal(snapshot, managedObject);
            wnd.ShowUtility();
        }

        void InspectInternal(PackedMemorySnapshot snapshot, PackedManagedObject managedObject)
        {
            m_Snapshot = snapshot;
            m_ManagedObject = managedObject;
            if (m_ManagedObject.address == 0)
            {
                m_ErrorString = "Cannot inspect 'null' address.";
                return;
            }


            var type = m_Snapshot.managedTypes[managedObject.managedTypesArrayIndex];

            titleContent = new GUIContent(string.Format("C# Object Inspector | {0} | {1:X}", type.name, managedObject.address));
            m_PropertyGrid = new PropertyGridControl(null, "ManagedObjectInspectorWindow.m_propertyGrid", new TreeViewState());
            m_PropertyGrid.Inspect(m_Snapshot, managedObject);
            m_ErrorString = "";
        }

        private void OnGUI()
        {
            if (!string.IsNullOrEmpty(m_ErrorString))
            {
                EditorGUILayout.HelpBox("The object cannot be inspected. Please see below for the reason.\n\n" + m_ErrorString, MessageType.Info);
                return;
            }

            m_PropertyGrid.OnGUI();
        }
    }
#endif
}
