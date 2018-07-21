using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;

namespace HeapExplorer
{
    public class GotoCommand
    {
        //public enum EKind
        //{
        //    None,
        //    GCHandle,
        //    NativeObject,
        //    ManagedObject,
        //    Overview,
        //    StaticField,
        //    StaticClass,
        //    ManagedObjectDuplicate,
        //    NativeObjectDuplicates
        //}

        public HeapExplorerView fromView;
        public HeapExplorerView toView;
        //public EKind toKind;
        public RichGCHandle toGCHandle;
        public RichManagedObject toManagedObject;
        public RichNativeObject toNativeObject;
        public RichStaticField toStaticField;
        public RichManagedType toManagedType;

        public GotoCommand()
        {
        }

        public GotoCommand(RichGCHandle value)
            : this()
        {
            //toKind = EKind.GCHandle;
            toGCHandle = value;
        }

        public GotoCommand(RichManagedObject value)
            : this()
        {
            //toKind = EKind.ManagedObject;
            toManagedObject = value;
        }

        public GotoCommand(RichNativeObject value)
            : this()
        {
            //toKind = EKind.NativeObject;
            toNativeObject = value;
        }

        public GotoCommand(RichStaticField value)
            : this()
        {
            //toKind = EKind.StaticField;
            toStaticField = value;
        }

        public GotoCommand(RichManagedType value)
            : this()
        {
            //toKind = EKind.StaticClass;
            toManagedType = value;
        }
    }

    public class GotoHistory
    {
        class Entry
        {
            public GotoCommand from;
            public GotoCommand to;
        }
        List<Entry> m_commands = new List<Entry>();
        int m_index;

        public void Add(GotoCommand from, GotoCommand to)
        {
            while (m_commands.Count > m_index && m_index >= 0)
                m_commands.RemoveAt(m_commands.Count - 1);

            var e = new Entry();
            e.from = from;
            e.to = to;
            m_commands.Add(e);
            m_index++;
        }

        public void Clear()
        {
            m_index = -1;
            m_commands.Clear();
        }

        public bool HasBack()
        {
            var i = m_index - 1;
            if (i < 0)
                return false;
            return true;
        }

        public GotoCommand Back()
        {
            if (!HasBack())
                return null;

            m_index--;
            return m_commands[m_index].from;
        }

        public bool HasForward()
        {
            var i = m_index;
            if (i >= m_commands.Count)
                return false;
            return true;
        }

        public GotoCommand Forward()
        {
            if (!HasForward())
                return null;

            var command = m_commands[m_index];
            m_index++;
            return command.to;
        }
    }

    public class ConnectionsControl : AbstractTreeView
    {
        //public System.Action<GotoCommand> gotoCB;

        public int count
        {
            get
            {
                if (rootItem != null && rootItem.hasChildren && rootItem.children[0].depth > rootItem.depth)
                    return rootItem.children.Count;
                return 0;
            }
        }

        PackedMemorySnapshot m_snapshot;
        PackedConnection[] m_connections;
        bool m_addFrom;
        bool m_addTo;
        int m_uniqueId = 1;

        enum Column
        {
            Type,
            Name,
            Address,
        }

        public ConnectionsControl(HeapExplorerWindow window, string editorPrefsKey, TreeViewState state)
            : base(window, editorPrefsKey, state, new MultiColumnHeader(
                new MultiColumnHeaderState(new[]
                {
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Type"), width = 200, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("C++ Name"), width = 200, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Address"), width = 200, autoResize = true },
                })))
        {
            extraSpaceBeforeIconAndLabel = 4;
            columnIndexForTreeFoldouts = 0;
            multiColumnHeader.canSort = false;

            Reload();
        }

        public TreeViewItem BuildTree(PackedMemorySnapshot snapshot, PackedConnection[] connections, bool addFrom, bool addTo)
        {
            m_snapshot = snapshot;
            m_connections = connections;
            m_addFrom = addFrom;
            m_addTo = addTo;

            m_uniqueId = 1;

            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
            if (m_snapshot == null || m_connections == null || m_connections.Length < 1)
            {
                root.AddChild(new TreeViewItem { id = 1, depth = -1, displayName = "" });
                return root;
            }

            for (int n = 0, nend = m_connections.Length; n < nend; ++n)
            {
                var connection = m_connections[n];

                if (m_addTo)
                {
                    switch (connection.toKind)
                    {
                        case PackedConnection.Kind.GCHandle:
                            AddGCHandle(root, m_snapshot.gcHandles[connection.to]);
                            break;

                        case PackedConnection.Kind.Managed:
                            AddManagedObject(root, m_snapshot.managedObjects[connection.to]);
                            break;

                        case PackedConnection.Kind.Native:
                            AddNativeUnityObject(root, m_snapshot.nativeObjects[connection.to]);
                            break;

                        case PackedConnection.Kind.StaticField:
                            AddStaticField(root, m_snapshot.managedStaticFields[connection.to]);
                            break;
                    }
                }

                if (m_addFrom)
                {
                    switch (connection.fromKind)
                    {
                        case PackedConnection.Kind.GCHandle:
                            AddGCHandle(root, m_snapshot.gcHandles[connection.from]);
                            break;

                        case PackedConnection.Kind.Managed:
                            AddManagedObject(root, m_snapshot.managedObjects[connection.from]);
                            break;

                        case PackedConnection.Kind.Native:
                            AddNativeUnityObject(root, m_snapshot.nativeObjects[connection.from]);
                            break;

                        case PackedConnection.Kind.StaticField:
                            AddStaticField(root, m_snapshot.managedStaticFields[connection.from]);
                            break;
                    }
                }
            }

            //if (root.hasChildren)
            //{
            //    root.children.Sort(delegate (TreeViewItem x, TreeViewItem y)
            //    {
            //        var xx = x as Item;
            //        var yy = y as Item;

            //        return xx.m_address.CompareTo(yy.m_address);
            //    });
            //}

            return root;
        }
        
        void AddGCHandle(TreeViewItem parent, PackedGCHandle gcHandle)
        {
            var item = new GCHandleItem
            {
                id = m_uniqueId++,
                depth = parent.depth + 1
            };

            item.Initialize(this, m_snapshot, gcHandle.gcHandlesArrayIndex);
            parent.AddChild(item);
        }

        void AddManagedObject(TreeViewItem parent, PackedManagedObject managedObject)
        {
            var item = new ManagedObjectItem
            {
                id = m_uniqueId++,
                depth = parent.depth + 1
            };

            item.Initialize(this, m_snapshot, managedObject.managedObjectsArrayIndex);
            parent.AddChild(item);
        }

        void AddNativeUnityObject(TreeViewItem parent, PackedNativeUnityEngineObject nativeObject)
        {
            var item = new NativeObjectItem
            {
                id = m_uniqueId++,
                depth = parent.depth + 1,
            };

            item.Initialize(this, m_snapshot, nativeObject);
            parent.AddChild(item);
        }

        void AddStaticField(TreeViewItem parent, PackedManagedStaticField staticField)
        {
            var item = new ManagedStaticFieldItem
            {
                id = m_uniqueId++,
                depth = parent.depth + 1,
            };

            item.Initialize(this, m_snapshot, staticField.staticFieldsArrayIndex);
            parent.AddChild(item);
        }

        protected override int OnSortItem(TreeViewItem x, TreeViewItem y)
        {
            return 0;
        }

        ///////////////////////////////////////////////////////////////////////////
        // TreeViewItem's
        ///////////////////////////////////////////////////////////////////////////

        class Item : AbstractTreeViewItem
        {
            protected ConnectionsControl m_owner;
            protected string m_value;
            protected string m_tooltip = "";
            public System.UInt64 m_address;

            public override void GetItemSearchString(string[] target, out int count)
            {
                count = 0;
                target[count++] = displayName;
                target[count++] = m_value;
                target[count++] = string.Format(StringFormat.Address, m_address);
            }

            public override void OnGUI(Rect position, int column)
            {
                switch ((Column)column)
                {
                    case Column.Type:
                        HeEditorGUI.TypeName(position, displayName, m_tooltip);
                        break;

                    case Column.Name:
                        EditorGUI.LabelField(position, m_value);
                        break;

                    case Column.Address:
                        if (m_address != 0) // statics dont have an address in PackedMemorySnapshot and I don't want to display a misleading 0
                            HeEditorGUI.Address(position, m_address);
                        break;
                }
            }
        }

        // ------------------------------------------------------------------------

        class GCHandleItem : Item
        {
            PackedMemorySnapshot m_snapshot;
            RichGCHandle m_gcHandle;

            public void Initialize(ConnectionsControl owner, PackedMemorySnapshot snapshot, int gcHandleArrayIndex)
            {
                m_owner = owner;
                m_snapshot = snapshot;
                m_gcHandle = new RichGCHandle(m_snapshot, gcHandleArrayIndex);

                displayName = "GCHandle";
                m_value = m_gcHandle.managedObject.isValid ? m_gcHandle.managedObject.type.name : "";
                m_address = m_gcHandle.managedObjectAddress;
            }

            public override void OnGUI(Rect position, int column)
            {
                if (column == 0)
                {
                    if (HeEditorGUI.GCHandleButton(HeEditorGUI.SpaceL(ref position, position.height)))
                    {
                        m_owner.window.OnGoto(new GotoCommand(m_gcHandle));
                    }

                    if (m_gcHandle.nativeObject.isValid)
                    {
                        if (HeEditorGUI.CppButton(HeEditorGUI.SpaceR(ref position, position.height)))
                        {
                            m_owner.window.OnGoto(new GotoCommand(m_gcHandle.nativeObject));
                        }
                    }

                    if (m_gcHandle.managedObject.isValid)
                    {
                        if (HeEditorGUI.CsButton(HeEditorGUI.SpaceR(ref position, position.height)))
                        {
                            m_owner.window.OnGoto(new GotoCommand(m_gcHandle.managedObject));
                        }
                    }
                }

                base.OnGUI(position, column);
            }
        }

        // ------------------------------------------------------------------------

        class ManagedObjectItem : Item
        {
            //PackedMemorySnapshot m_snapshot;
            RichManagedObject m_managedObject;

            public void Initialize(ConnectionsControl owner, PackedMemorySnapshot snapshot, int arrayIndex)
            {
                m_owner = owner;
                //m_snapshot = snapshot;
                m_managedObject = new RichManagedObject(snapshot, arrayIndex);

                displayName = m_managedObject.type.name;
                m_address = m_managedObject.address;
                m_value = m_managedObject.nativeObject.isValid ? m_managedObject.nativeObject.name : "";
                m_tooltip = PackedManagedTypeUtility.GetInheritanceAsString(snapshot, m_managedObject.type.packed.managedTypesArrayIndex);
            }

            public override void OnGUI(Rect position, int column)
            {
                if (column == 0)
                {
                    if (m_managedObject.gcHandle.isValid)
                    {
                        if (HeEditorGUI.GCHandleButton(HeEditorGUI.SpaceR(ref position, position.height)))
                        {
                            m_owner.window.OnGoto(new GotoCommand(m_managedObject.gcHandle));
                        }
                    }

                    if (HeEditorGUI.CsButton(HeEditorGUI.SpaceL(ref position, position.height)))
                    {
                        m_owner.window.OnGoto(new GotoCommand(m_managedObject));
                    }

                    if (m_managedObject.nativeObject.isValid)
                    {
                        if (HeEditorGUI.CppButton(HeEditorGUI.SpaceR(ref position, position.height)))
                        {
                            m_owner.window.OnGoto(new GotoCommand(m_managedObject.nativeObject));
                        }
                    }
                }

                base.OnGUI(position, column);
            }
        }

        // ------------------------------------------------------------------------

        class ManagedStaticFieldItem : Item
        {
            PackedMemorySnapshot m_snapshot;
            PackedManagedStaticField m_staticField;

            public void Initialize(ConnectionsControl owner, PackedMemorySnapshot snapshot, int arrayIndex)
            {
                m_owner = owner;
                m_snapshot = snapshot;
                m_staticField = m_snapshot.managedStaticFields[arrayIndex];

                displayName = m_snapshot.managedTypes[m_staticField.managedTypesArrayIndex].name;
                m_address = 0;
                m_value = "static " + m_snapshot.managedTypes[m_staticField.managedTypesArrayIndex].name + "." + m_snapshot.managedTypes[m_staticField.managedTypesArrayIndex].fields[m_staticField.fieldIndex].name;
            }

            public override void OnGUI(Rect position, int column)
            {
                if (column == 0)
                {
                    if (HeEditorGUI.CsStaticButton(HeEditorGUI.SpaceL(ref position, position.height)))
                    {
                        m_owner.window.OnGoto(new GotoCommand(new RichStaticField(m_snapshot, m_staticField.staticFieldsArrayIndex)));
                    }
                }

                base.OnGUI(position, column);
            }
        }

        // ------------------------------------------------------------------------

        class NativeObjectItem : Item
        {
            PackedMemorySnapshot m_snapshot;
            RichNativeObject m_nativeObject;

            public void Initialize(ConnectionsControl owner, PackedMemorySnapshot snapshot, PackedNativeUnityEngineObject nativeObject)
            {
                m_owner = owner;
                m_snapshot = snapshot;
                m_nativeObject = new RichNativeObject(snapshot, nativeObject.nativeObjectsArrayIndex);

                m_value = m_nativeObject.name;
                m_address = m_nativeObject.address;
                displayName = m_nativeObject.type.name;

                // If it's a MonoBehaviour or ScriptableObject, use the C# typename instead
                // It makes it easier to understand what it is, otherwise everything displays 'MonoBehaviour' only.
                if (m_nativeObject.type.IsSubclassOf(m_snapshot.coreTypes.nativeMonoBehaviour) || m_nativeObject.type.IsSubclassOf(m_snapshot.coreTypes.nativeScriptableObject))
                {
                    string monoScriptName;
                    if (m_snapshot.FindNativeMonoScriptType(m_nativeObject.packed.nativeObjectsArrayIndex, out monoScriptName) != -1)
                    {
                        if (!string.IsNullOrEmpty(monoScriptName))
                            displayName = monoScriptName;
                    }
                }
            }

            public override void OnGUI(Rect position, int column)
            {
                if (column == 0)
                {
                    if (HeEditorGUI.CppButton(HeEditorGUI.SpaceL(ref position, position.height)))
                    {
                        m_owner.window.OnGoto(new GotoCommand(m_nativeObject));
                    }

                    if (m_nativeObject.gcHandle.isValid)
                    {
                        if (HeEditorGUI.GCHandleButton(HeEditorGUI.SpaceR(ref position, position.height)))
                        {
                            m_owner.window.OnGoto(new GotoCommand(m_nativeObject.gcHandle));
                        }
                    }

                    if (m_nativeObject.managedObject.isValid)
                    {
                        if (HeEditorGUI.CsButton(HeEditorGUI.SpaceR(ref position, position.height)))
                        {
                            m_owner.window.OnGoto(new GotoCommand(m_nativeObject.managedObject));
                        }
                    }
                }

                base.OnGUI(position, column);
            }
        }
    }

    public static class TreeViewUtility
    {
        public static Rect IndentByDepth(TreeViewItem item, Rect rect)
        {
            //rect.y += 2;
            //rect.height -= 2;

            //if (item.hasChildren)
            //if (item.parent !)
            {
                var foldoutWidth = 14;
                var indent = item.depth;
                //if (item.hasChildren)
                indent++;

                rect.x += indent * foldoutWidth;
                rect.width -= indent * foldoutWidth;
            }

            return rect;
        }
    }
}
