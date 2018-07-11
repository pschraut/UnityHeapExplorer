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
            m_editorPrefsKey = "HeapExplorer.ManagedDelegateTargetsView";
        }

        protected override AbstractManagedObjectsControl CreateObjectsTreeView(string editorPrefsKey, TreeViewState state)
        {
            return new ManagedDelegateTargetsControl(window, editorPrefsKey, state);
        }

        protected override void OnDrawHeader()
        {
            var text = string.Format("{0} managed object(s), {1} memory", m_objectsControl.managedObjectsCount, EditorUtility.FormatBytes(m_objectsControl.managedObjectsSize));
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

            var reader = new MemoryReader(m_snapshot);
            var systemDelegate = m_snapshot.managedTypes[m_snapshot.coreTypes.systemDelegate];

            PackedManagedField field;
            if (!systemDelegate.TryGetField("m_target", out field))
                return;

            // Build a table that contains indices of all objects that are the "Target" of a delegate
            for (int n = 0, nend = m_snapshot.managedObjects.Length; n < nend; ++n)
            {
                var obj = m_snapshot.managedObjects[n];
                if (obj.address == 0)
                    continue;

                // Is this a System.Delegate?
                var type = m_snapshot.managedTypes[obj.managedTypesArrayIndex];
                if (!m_snapshot.IsSubclassOf(type, m_snapshot.coreTypes.systemDelegate))
                    continue;

                // Read the delegate m_target pointer
                var pointer = reader.ReadPointer(obj.address + (uint)field.offset);
                if (pointer == 0)
                    continue;

                // Try to find the managed object where m_target points to
                var target = m_snapshot.FindManagedObjectOfAddress(pointer);
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
            m_editorPrefsKey = "HeapExplorer.ManagedDelegatesView";
        }

        protected override AbstractManagedObjectsControl CreateObjectsTreeView(string editorPrefsKey, TreeViewState state)
        {
            return new ManagedDelegatesControl(window, editorPrefsKey, state);
        }

        protected override void OnDrawHeader()
        {
            var text = string.Format("{0} delegate(s), {1} memory", m_objectsControl.managedObjectsCount, EditorUtility.FormatBytes(m_objectsControl.managedObjectsSize));
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
            var type = m_snapshot.managedTypes[mo.managedTypesArrayIndex];
            if (!m_snapshot.IsSubclassOf(type, m_snapshot.coreTypes.systemDelegate))
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
            m_editorPrefsKey = "HeapExplorer.ManagedObjectsView";
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
            var text = string.Format("{0} managed object(s), {1} memory", m_objectsControl.managedObjectsCount, EditorUtility.FormatBytes(m_objectsControl.managedObjectsSize));
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
        protected AbstractManagedObjectsControl m_objectsControl;
        protected string m_editorPrefsKey;

        HeSearchField m_objectsSearch;
        ConnectionsView m_connectionsView;
        RichManagedObject m_selected;
        RootPathView m_rootPathView;
        PropertyGridView m_propertyGridView;
        float m_splitterHorzPropertyGrid = 0.32f;
        float m_splitterVertConnections = 0.3333f;
        float m_splitterVertRootPath = 0.3333f;

        public override void Awake()
        {
            base.Awake();

            titleContent = new GUIContent("C# Objects", "");
            m_editorPrefsKey = "HeapExplorer.AbstractManagedObjectsView";
        }

        abstract protected AbstractManagedObjectsControl CreateObjectsTreeView(string editorPrefsKey, TreeViewState state);

        abstract protected void OnDrawHeader();

        protected override void OnCreate()
        {
            base.OnCreate();

            m_connectionsView = CreateView<ConnectionsView>();
            m_connectionsView.editorPrefsKey = m_editorPrefsKey + ".m_connectionsView";

            m_rootPathView = CreateView<RootPathView>();
            m_rootPathView.editorPrefsKey = m_editorPrefsKey + ".m_rootPathView";

            m_propertyGridView = CreateView<PropertyGridView>();
            m_propertyGridView.editorPrefsKey = m_editorPrefsKey + ".m_propertyGridView";

            m_objectsControl = CreateObjectsTreeView(m_editorPrefsKey + ".m_objects", new TreeViewState());// new ManagedObjectsControl(m_editorPrefsKey + ".m_objects", new TreeViewState());
            m_objectsControl.onSelectionChange += OnListViewSelectionChange;
            //m_objectsControl.gotoCB += Goto;
            m_objectsControl.SetTree(m_objectsControl.BuildTree(snapshot));

            m_objectsSearch = new HeSearchField(window);
            m_objectsSearch.downOrUpArrowKeyPressed += m_objectsControl.SetFocusAndEnsureSelectedItem;
            m_objectsControl.findPressed += m_objectsSearch.SetFocus;

            m_splitterHorzPropertyGrid = EditorPrefs.GetFloat(m_editorPrefsKey + ".m_splitterHorzPropertyGrid", m_splitterHorzPropertyGrid);
            m_splitterVertConnections = EditorPrefs.GetFloat(m_editorPrefsKey + ".m_splitterVertConnections", m_splitterVertConnections);
            m_splitterVertRootPath = EditorPrefs.GetFloat(m_editorPrefsKey + ".m_splitterVertRootPath", m_splitterVertRootPath);
        }

        protected override void OnHide()
        {
            base.OnHide();

            m_objectsControl.SaveLayout();

            EditorPrefs.SetFloat(m_editorPrefsKey + ".m_splitterHorzPropertyGrid", m_splitterHorzPropertyGrid);
            EditorPrefs.SetFloat(m_editorPrefsKey + ".m_splitterVertConnections", m_splitterVertConnections);
            EditorPrefs.SetFloat(m_editorPrefsKey + ".m_splitterVertRootPath", m_splitterVertRootPath);
        }

        public override void RestoreCommand(GotoCommand command)
        {
            if (command.toManagedObject.isValid)
                m_objectsControl.Select(command.toManagedObject.packed);

            base.RestoreCommand(command);
        }

        public override GotoCommand GetRestoreCommand()
        {
            if (m_selected.isValid)
                return new GotoCommand(m_selected);

            return base.GetRestoreCommand();
        }
        
        void OnListViewSelectionChange(PackedManagedObject? item)
        {
            m_selected = RichManagedObject.invalid;
            if (!item.HasValue)
            {
                m_rootPathView.Clear();
                m_connectionsView.Clear();
                m_propertyGridView.Clear();
                return;
            }

            m_selected = new RichManagedObject(snapshot, item.Value.managedObjectsArrayIndex);
            m_connectionsView.Inspect(m_selected.packed);
            m_rootPathView.Inspect(m_selected.packed);
            m_propertyGridView.Inspect(m_selected.packed);
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

                            if (m_objectsSearch.OnToolbarGUI())
                                m_objectsControl.Search(m_objectsSearch.text);
                        }
                        GUILayout.Space(2);

                        UnityEngine.Profiling.Profiler.BeginSample("m_objectsControl.OnGUI");
                        m_objectsControl.OnGUI();
                        UnityEngine.Profiling.Profiler.EndSample();
                    }

                    HeEditorGUILayout.VerticalSplitter("m_splitterVertConnections".GetHashCode(), ref m_splitterVertConnections, 0.1f, 0.8f, window);

                    using (new EditorGUILayout.HorizontalScope(GUILayout.Height(window.position.height * m_splitterVertConnections)))
                    {
                        m_connectionsView.OnGUI();
                    }
                }

                HeEditorGUILayout.HorizontalSplitter("m_splitterHorzPropertyGrid".GetHashCode(), ref m_splitterHorzPropertyGrid, 0.1f, 0.6f, window);

                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel, GUILayout.Width(window.position.width * m_splitterHorzPropertyGrid)))
                    {
                        m_propertyGridView.OnGUI();
                    }

                    HeEditorGUILayout.VerticalSplitter("m_splitterVertRootPath".GetHashCode(), ref m_splitterVertRootPath, 0.1f, 0.8f, window);

                    using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel, GUILayout.Width(window.position.width * m_splitterHorzPropertyGrid), GUILayout.Height(window.position.height * m_splitterVertRootPath)))
                    {
                        m_rootPathView.OnGUI();
                    }
                }
            }
        }
    }
}
