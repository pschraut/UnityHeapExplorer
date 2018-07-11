using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;

namespace HeapExplorer
{
    public class PropertyGridControl : AbstractTreeView
    {
        //public System.Action<GotoCommand> gotoCB;
        
        PackedMemorySnapshot m_snapshot;
        EditorWindow m_window;
        string m_splitterName;

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
            m_window = window;
            m_splitterName = editorPrefsKey + ".m_splitterHorzPropertyGrid";
            m_splitterHorzPropertyGrid = EditorPrefs.GetFloat(editorPrefsKey + m_splitterName, m_splitterHorzPropertyGrid);
            Reload();
        }

        protected override bool CanChangeExpandedState(TreeViewItem itm)
        {
            var item = itm as PropertyGridItem;
            if (item != null)
                return item.allowExpand;

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
            m_snapshot = snapshot;
            var m_type = snapshot.managedTypes[managedObject.managedTypesArrayIndex];
            var m_address = managedObject.address;
            if (m_type.isValueType)
                m_address -= (ulong)m_snapshot.virtualMachineInformation.objectHeaderSize;

            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
            if (m_snapshot == null)
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
            m_snapshot = snapshot;
            var m_type = managedType;
            var m_address = 0ul;

            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
            if (m_snapshot == null)
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
            m_snapshot = null;
            SetTree(null);
            m_dataVisualizer = null;
            m_dataVisualizerScrollPos = new Vector2();
        }

        public override void OnGUI()
        {
            base.OnGUI();

            DrawDataVisualizer();
        }

        protected override void OnSelectionChanged(TreeViewItem selectedItem)
        {
            base.OnSelectionChanged(selectedItem);

            m_dataVisualizer = null;
            m_dataVisualizerScrollPos = new Vector2();
            var item = selectedItem as PropertyGridItem;
            if (item != null)
                TryCreateDataVisualizer(item.myMemoryReader, item.m_type, item.address, true);
        }

        AbstractDataVisualizer m_dataVisualizer;
        Vector2 m_dataVisualizerScrollPos;
        float m_splitterHorzPropertyGrid = 0.15f;

        void TryCreateDataVisualizer(AbstractMemoryReader reader, PackedManagedType type, ulong address, bool resolveAddress)
        {
            m_dataVisualizer = null;
            m_dataVisualizerScrollPos = new Vector2();
            
            //if (AbstractDataVisualizer.HasVisualizer(type.name))
            {
                if (type.isPointer && resolveAddress)
                    address = reader.ReadPointer(address);

                m_dataVisualizer = AbstractDataVisualizer.CreateVisualizer(type.name);
                m_dataVisualizer.Initialize(m_snapshot, reader, address, type);
            }
        }

        void DrawDataVisualizer()
        {
            if (m_dataVisualizer == null)
                return;

            GUILayout.Space(2);
            HeEditorGUILayout.VerticalSplitter(m_splitterName.GetHashCode(), ref m_splitterHorzPropertyGrid, 0.1f, 0.6f, m_window);
            GUILayout.Space(2);

            using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel, GUILayout.Height(m_window.position.height * m_splitterHorzPropertyGrid)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    // We draw a dataVisualizer always, but don't display the content of the fallback visualizer.
                    // This is to keep the UI free from hiding/showing the visualizer panel and keep it more stable.
                    // TODO: This is actually a HACK until I fixed the default preview
                    if (!m_dataVisualizer.isFallback)
                        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

                    if (m_dataVisualizer.hasMenu)
                    {
                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button(new GUIContent("", "Open menu"), HeEditorStyles.paneOptions, GUILayout.Width(20)))
                        {
                            m_dataVisualizer.ShowMenu();
                        }
                    }
                }

                GUILayout.Space(4);

                using (var scrollView = new EditorGUILayout.ScrollViewScope(m_dataVisualizerScrollPos))
                {
                    m_dataVisualizerScrollPos = scrollView.scrollPosition;

                    m_dataVisualizer.GUI();
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
                var baseType = m_snapshot.managedTypes[type.baseOrElementTypeIndex];
                var isSystemObject = baseType.managedTypesArrayIndex == m_snapshot.coreTypes.systemObject;
                if (!isSystemObject && PackedManagedTypeUtility.HasTypeOrBaseAnyField(m_snapshot, baseType, !addStatic, addStatic))
                {
                    var item = new BaseClassPropertyGridItem(this, m_snapshot, address, reader)
                    {
                        depth = target.depth + 1,
                        baseClassTypeIndex = baseType.managedTypesArrayIndex
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
                var item = new ArrayPropertyGridItem(this, m_snapshot, pointer, reader)
                {
                    depth = target.depth + 1,
                    arrayType = type,
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

                var fieldType = m_snapshot.managedTypes[type.fields[n].managedTypesArrayIndex];

                // Array
                if (fieldType.isArray)
                {
                    if (fieldType.baseOrElementTypeIndex == -1)
                        continue;

                    var pointer = reader.ReadPointer(address + (ulong)type.fields[n].offset);
                    var item = new ArrayPropertyGridItem(this, m_snapshot, pointer, new MemoryReader(m_snapshot))
                    {
                        depth = target.depth + 1,
                        arrayType = fieldType,
                        displayName = type.fields[n].name
                    };
                    item.Initialize();

                    target.AddChild(item);
                    continue;
                }

                // Primitive types and types derived from System.Enum
                if (fieldType.isValueType && (fieldType.isPrimitive || m_snapshot.IsEnum(fieldType)))
                {
                    var item = new PrimitiveTypePropertyGridItem(this, m_snapshot, address + (ulong)type.fields[n].offset, reader)
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
                    var item = new ValueTypePropertyGridItem(this, m_snapshot, address + (ulong)type.fields[n].offset, reader)
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
                    var item = new ReferenceTypePropertyGridItem(this, m_snapshot, address + (ulong)type.fields[n].offset, reader)
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










    public class ManagedObjectInspectorWindow : EditorWindow
    {
        PropertyGridControl m_propertyGrid;
        PackedMemorySnapshot m_snapshot;
        //ulong m_address;
        string m_errorString = "";
        PackedManagedObject m_managedObject;

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
            m_snapshot = snapshot;
            m_managedObject = managedObject;
            if (m_managedObject.address == 0)
            {
                m_errorString = "Cannot inspect 'null' address.";
                return;
            }


            var type = m_snapshot.managedTypes[managedObject.managedTypesArrayIndex];

            titleContent = new GUIContent(string.Format("C# Object Inspector | {0} | {1:X}", type.name, managedObject.address));
            m_propertyGrid = new PropertyGridControl(null, "ManagedObjectInspectorWindow.m_propertyGrid", new TreeViewState());
            m_propertyGrid.Inspect(m_snapshot, managedObject);
            m_errorString = "";
        }

        private void OnGUI()
        {
            if (!string.IsNullOrEmpty(m_errorString))
            {
                EditorGUILayout.HelpBox("The object cannot be inspected. Please see below for the reason.\n\n" + m_errorString, MessageType.Info);
                return;
            }

            m_propertyGrid.OnGUI();
        }
    }
}
