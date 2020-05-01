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
    public class ManagedDelegateTargetsView : AbstractManagedObjectsView
    {
        [InitializeOnLoadMethod]
        static void Register()
        {
            HeapExplorerWindow.Register<ManagedDelegateTargetsView>();
        }

        public override void Awake()
        {
            base.Awake();

            titleContent = new GUIContent("C# Delegate Targets", "");
            viewMenuOrder = 265;
        }

        protected override AbstractManagedObjectsControl CreateObjectsTreeView(string editorPrefsKey, TreeViewState state)
        {
            return new ManagedDelegateTargetsControl(window, editorPrefsKey, state);
        }

        protected override void OnDrawHeader()
        {
            var text = string.Format("{0} managed object(s), {1} memory", m_ObjectsControl.managedObjectsCount, EditorUtility.FormatBytes(m_ObjectsControl.managedObjectsSize));
            window.SetStatusbarString(text);
            EditorGUILayout.LabelField(titleContent, EditorStyles.boldLabel);
        }
    }

    public class ManagedDelegateTargetsControl : AbstractManagedObjectsControl
    {
        Dictionary<int, byte> m_delegateObjectTable = new Dictionary<int, byte>(64);

        public ManagedDelegateTargetsControl(HeapExplorerWindow window, string editorPrefsKey, TreeViewState state)
            : base(window, editorPrefsKey, state)
        {
        }

        protected override void OnBeforeBuildTree()
        {
            // This method builds a lookup table of objects that are
            // references by the m_target field of a System.Delegate object.

            var reader = new MemoryReader(m_Snapshot);
            var systemDelegate = m_Snapshot.managedTypes[m_Snapshot.coreTypes.systemDelegate];

            PackedManagedField field;
            if (!systemDelegate.TryGetField("m_target", out field))
                return;

            // Build a table that contains indices of all objects that are the "Target" of a delegate
            for (int n = 0, nend = m_Snapshot.managedObjects.Length; n < nend; ++n)
            {
                var obj = m_Snapshot.managedObjects[n];
                if (obj.address == 0)
                    continue;

                // Is this a System.Delegate?
                var type = m_Snapshot.managedTypes[obj.managedTypesArrayIndex];
                if (!m_Snapshot.IsSubclassOf(type, m_Snapshot.coreTypes.systemDelegate))
                    continue;

                // Read the delegate m_target pointer
                var pointer = reader.ReadPointer(obj.address + (uint)field.offset);
                if (pointer == 0)
                    continue;

                // Try to find the managed object where m_target points to
                var target = m_Snapshot.FindManagedObjectOfAddress(pointer);
                if (target < 0)
                    continue;

                // We found a managed object that is referenced by a System.Delegate
                m_delegateObjectTable[target] = 1;
            }
        }

        protected override bool OnCanAddObject(PackedManagedObject mo)
        {
            var value = m_delegateObjectTable.ContainsKey(mo.managedObjectsArrayIndex);
            return value;
        }
    }



    public class ManagedDelegatesView : AbstractManagedObjectsView
    {
        [InitializeOnLoadMethod]
        static void Register()
        {
            HeapExplorerWindow.Register<ManagedDelegatesView>();
        }

        public override void Awake()
        {
            base.Awake();

            titleContent = new GUIContent("C# Delegates", "");
            viewMenuOrder = 260;
        }

        protected override AbstractManagedObjectsControl CreateObjectsTreeView(string editorPrefsKey, TreeViewState state)
        {
            return new ManagedDelegatesControl(window, editorPrefsKey, state);
        }

        protected override void OnDrawHeader()
        {
            var text = string.Format("{0} delegate(s), {1} memory", m_ObjectsControl.managedObjectsCount, EditorUtility.FormatBytes(m_ObjectsControl.managedObjectsSize));
            window.SetStatusbarString(text);
            EditorGUILayout.LabelField(titleContent, EditorStyles.boldLabel);
        }
    }

    public class ManagedDelegatesControl : AbstractManagedObjectsControl
    {
        public ManagedDelegatesControl(HeapExplorerWindow window, string editorPrefsKey, TreeViewState state)
            : base(window, editorPrefsKey, state)
        {
        }

        protected override bool OnCanAddObject(PackedManagedObject mo)
        {
            var type = m_Snapshot.managedTypes[mo.managedTypesArrayIndex];
            if (!m_Snapshot.IsSubclassOf(type, m_Snapshot.coreTypes.systemDelegate))
                return false;

            return true;
        }
    }

    public class ManagedObjectsView : AbstractManagedObjectsView
    {
        [InitializeOnLoadMethod]
        static void Register()
        {
            HeapExplorerWindow.Register<ManagedObjectsView>();
        }

        public override void Awake()
        {
            base.Awake();

            titleContent = new GUIContent("C# Objects", "");
            viewMenuOrder = 250;
        }

        public override int CanProcessCommand(GotoCommand command)
        {
            if (command.toManagedObject.isValid)
                return 10;

            return base.CanProcessCommand(command);
        }

        protected override AbstractManagedObjectsControl CreateObjectsTreeView(string editorPrefsKey, TreeViewState state)
        {
            return new ManagedObjectsControl(window, editorPrefsKey, state);
        }

        protected override void OnDrawHeader()
        {
            var text = string.Format("{0} managed object(s), {1} memory", m_ObjectsControl.managedObjectsCount, EditorUtility.FormatBytes(m_ObjectsControl.managedObjectsSize));
            window.SetStatusbarString(text);
            EditorGUILayout.LabelField(titleContent, EditorStyles.boldLabel);
        }
    }

    public class ManagedObjectsControl : AbstractManagedObjectsControl
    {
        public ManagedObjectsControl(HeapExplorerWindow window, string editorPrefsKey, TreeViewState state)
            : base(window, editorPrefsKey, state)
        {
        }

        protected override bool OnCanAddObject(PackedManagedObject mo)
        {
            return true;
        }
    }

    public abstract class AbstractManagedObjectsView : HeapExplorerView
    {
        protected AbstractManagedObjectsControl m_ObjectsControl;

        HeSearchField m_ObjectsSearchField;
        ConnectionsView m_ConnectionsView;
        RichManagedObject m_Selected;
        RootPathView m_RootPathView;
        PropertyGridView m_PropertyGridView;
        float m_SplitterHorzPropertyGrid = 0.32f;
        float m_SplitterVertConnections = 0.3333f;
        float m_SplitterVertRootPath = 0.3333f;

        public override void Awake()
        {
            base.Awake();

            titleContent = new GUIContent("C# Objects", "");
        }

        abstract protected AbstractManagedObjectsControl CreateObjectsTreeView(string editorPrefsKey, TreeViewState state);

        abstract protected void OnDrawHeader();

        protected override void OnCreate()
        {
            base.OnCreate();

            m_ConnectionsView = CreateView<ConnectionsView>();
            m_ConnectionsView.editorPrefsKey = GetPrefsKey(() => m_ConnectionsView);

            m_RootPathView = CreateView<RootPathView>();
            m_RootPathView.editorPrefsKey = GetPrefsKey(() => m_RootPathView);

            m_PropertyGridView = CreateView<PropertyGridView>();
            m_PropertyGridView.editorPrefsKey = GetPrefsKey(() => m_PropertyGridView);

            m_ObjectsControl = CreateObjectsTreeView(GetPrefsKey(() => m_ObjectsControl), new TreeViewState());
            m_ObjectsControl.onSelectionChange += OnListViewSelectionChange;
            m_ObjectsControl.SetTree(m_ObjectsControl.BuildTree(snapshot));

            m_ObjectsSearchField = new HeSearchField(window);
            m_ObjectsSearchField.downOrUpArrowKeyPressed += m_ObjectsControl.SetFocusAndEnsureSelectedItem;
            m_ObjectsControl.findPressed += m_ObjectsSearchField.SetFocus;

            m_SplitterHorzPropertyGrid = EditorPrefs.GetFloat(GetPrefsKey(() => m_SplitterHorzPropertyGrid), m_SplitterHorzPropertyGrid);
            m_SplitterVertConnections = EditorPrefs.GetFloat(GetPrefsKey(() => m_SplitterVertConnections), m_SplitterVertConnections);
            m_SplitterVertRootPath = EditorPrefs.GetFloat(GetPrefsKey(() => m_SplitterVertRootPath), m_SplitterVertRootPath);
        }

        protected override void OnHide()
        {
            base.OnHide();

            m_ObjectsControl.SaveLayout();

            EditorPrefs.SetFloat(GetPrefsKey(() => m_SplitterHorzPropertyGrid), m_SplitterHorzPropertyGrid);
            EditorPrefs.SetFloat(GetPrefsKey(() => m_SplitterVertConnections), m_SplitterVertConnections);
            EditorPrefs.SetFloat(GetPrefsKey(() => m_SplitterVertRootPath), m_SplitterVertRootPath);
        }

        public override void RestoreCommand(GotoCommand command)
        {
            if (command.toManagedObject.isValid)
                m_ObjectsControl.Select(command.toManagedObject.packed);

            base.RestoreCommand(command);
        }

        public override GotoCommand GetRestoreCommand()
        {
            if (m_Selected.isValid)
                return new GotoCommand(m_Selected);

            return base.GetRestoreCommand();
        }
        
        void OnListViewSelectionChange(PackedManagedObject? item)
        {
            m_Selected = RichManagedObject.invalid;
            if (!item.HasValue)
            {
                m_RootPathView.Clear();
                m_ConnectionsView.Clear();
                m_PropertyGridView.Clear();
                return;
            }

            m_Selected = new RichManagedObject(snapshot, item.Value.managedObjectsArrayIndex);
            m_ConnectionsView.Inspect(m_Selected.packed);
            m_PropertyGridView.Inspect(m_Selected.packed);
            m_RootPathView.Inspect(m_Selected.packed);
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
                            OnDrawHeader();

                            if (m_ObjectsSearchField.OnToolbarGUI())
                                m_ObjectsControl.Search(m_ObjectsSearchField.text);
                        }
                        GUILayout.Space(2);

                        UnityEngine.Profiling.Profiler.BeginSample("m_objectsControl.OnGUI");
                        m_ObjectsControl.OnGUI();
                        UnityEngine.Profiling.Profiler.EndSample();
                    }

                    m_SplitterVertConnections = HeEditorGUILayout.VerticalSplitter("m_splitterVertConnections".GetHashCode(), m_SplitterVertConnections, 0.1f, 0.8f, window);

                    using (new EditorGUILayout.HorizontalScope(GUILayout.Height(window.position.height * m_SplitterVertConnections)))
                    {
                        m_ConnectionsView.OnGUI();
                    }
                }

                m_SplitterHorzPropertyGrid = HeEditorGUILayout.HorizontalSplitter("m_splitterHorzPropertyGrid".GetHashCode(), m_SplitterHorzPropertyGrid, 0.1f, 0.6f, window);

                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel, GUILayout.Width(window.position.width * m_SplitterHorzPropertyGrid)))
                    {
                        m_PropertyGridView.OnGUI();
                    }

                    m_SplitterVertRootPath = HeEditorGUILayout.VerticalSplitter("m_splitterVertRootPath".GetHashCode(), m_SplitterVertRootPath, 0.1f, 0.8f, window);

                    using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel, GUILayout.Width(window.position.width * m_SplitterHorzPropertyGrid), GUILayout.Height(window.position.height * m_SplitterVertRootPath)))
                    {
                        m_RootPathView.OnGUI();
                    }
                }
            }
        }
    }
}
