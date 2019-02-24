//
// Heap Explorer for Unity. Copyright (c) 2019 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://bitbucket.org/pschraut/unityheapexplorer/
//
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;

namespace HeapExplorer
{
    public class GotoCommand
    {
        public HeapExplorerView fromView;
        public HeapExplorerView toView;
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
            toGCHandle = value;
        }

        public GotoCommand(RichManagedObject value)
            : this()
        {
            toManagedObject = value;
        }

        public GotoCommand(RichNativeObject value)
            : this()
        {
            toNativeObject = value;
        }

        public GotoCommand(RichStaticField value)
            : this()
        {
            toStaticField = value;
        }

        public GotoCommand(RichManagedType value)
            : this()
        {
            toManagedType = value;
        }
    }

    public class GotoHistory
    {
        int m_Index;
        List<Entry> m_Commands = new List<Entry>();

        class Entry
        {
            public GotoCommand from;
            public GotoCommand to;
        }

        public void Add(GotoCommand from, GotoCommand to)
        {
            while (m_Commands.Count > m_Index && m_Index >= 0)
                m_Commands.RemoveAt(m_Commands.Count - 1);

            var e = new Entry();
            e.from = from;
            e.to = to;
            m_Commands.Add(e);
            m_Index++;
        }

        public void Clear()
        {
            m_Index = -1;
            m_Commands.Clear();
        }

        public bool HasBack()
        {
            var i = m_Index - 1;
            if (i < 0)
                return false;
            return true;
        }

        public GotoCommand Back()
        {
            if (!HasBack())
                return null;

            m_Index--;
            return m_Commands[m_Index].from;
        }

        public bool HasForward()
        {
            var i = m_Index;
            if (i >= m_Commands.Count)
                return false;
            return true;
        }

        public GotoCommand Forward()
        {
            if (!HasForward())
                return null;

            var command = m_Commands[m_Index];
            m_Index++;
            return command.to;
        }
    }

    public class ConnectionsControl : AbstractTreeView
    {
        public int count
        {
            get
            {
                if (rootItem != null && rootItem.hasChildren && rootItem.children[0].depth > rootItem.depth)
                    return rootItem.children.Count;
                return 0;
            }
        }

        PackedMemorySnapshot m_Snapshot;
        PackedConnection[] m_Connections;
        bool m_AddFrom;
        bool m_AddTo;
        int m_UniqueId = 1;

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
            m_Snapshot = snapshot;
            m_Connections = connections;
            m_AddFrom = addFrom;
            m_AddTo = addTo;

            m_UniqueId = 1;

            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
            if (m_Snapshot == null || m_Connections == null || m_Connections.Length < 1)
            {
                root.AddChild(new TreeViewItem { id = 1, depth = -1, displayName = "" });
                return root;
            }

            for (int n = 0, nend = m_Connections.Length; n < nend; ++n)
            {
                var connection = m_Connections[n];

                if (m_AddTo)
                {
                    switch (connection.toKind)
                    {
                        case PackedConnection.Kind.GCHandle:
                            AddGCHandle(root, m_Snapshot.gcHandles[connection.to]);
                            break;

                        case PackedConnection.Kind.Managed:
                            AddManagedObject(root, m_Snapshot.managedObjects[connection.to]);
                            break;

                        case PackedConnection.Kind.Native:
                            AddNativeUnityObject(root, m_Snapshot.nativeObjects[connection.to]);
                            break;

                        case PackedConnection.Kind.StaticField:
                            AddStaticField(root, m_Snapshot.managedStaticFields[connection.to]);
                            break;
                    }
                }

                if (m_AddFrom)
                {
                    switch (connection.fromKind)
                    {
                        case PackedConnection.Kind.GCHandle:
                            AddGCHandle(root, m_Snapshot.gcHandles[connection.from]);
                            break;

                        case PackedConnection.Kind.Managed:
                            AddManagedObject(root, m_Snapshot.managedObjects[connection.from]);
                            break;

                        case PackedConnection.Kind.Native:
                            AddNativeUnityObject(root, m_Snapshot.nativeObjects[connection.from]);
                            break;

                        case PackedConnection.Kind.StaticField:
                            AddStaticField(root, m_Snapshot.managedStaticFields[connection.from]);
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
                id = m_UniqueId++,
                depth = parent.depth + 1
            };

            item.Initialize(this, m_Snapshot, gcHandle.gcHandlesArrayIndex);
            parent.AddChild(item);
        }

        void AddManagedObject(TreeViewItem parent, PackedManagedObject managedObject)
        {
            var item = new ManagedObjectItem
            {
                id = m_UniqueId++,
                depth = parent.depth + 1
            };

            item.Initialize(this, m_Snapshot, managedObject.managedObjectsArrayIndex);
            parent.AddChild(item);
        }

        void AddNativeUnityObject(TreeViewItem parent, PackedNativeUnityEngineObject nativeObject)
        {
            var item = new NativeObjectItem
            {
                id = m_UniqueId++,
                depth = parent.depth + 1,
            };

            item.Initialize(this, m_Snapshot, nativeObject);
            parent.AddChild(item);
        }

        void AddStaticField(TreeViewItem parent, PackedManagedStaticField staticField)
        {
            var item = new ManagedStaticFieldItem
            {
                id = m_UniqueId++,
                depth = parent.depth + 1,
            };

            item.Initialize(this, m_Snapshot, staticField.staticFieldsArrayIndex);
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
            public System.UInt64 address;

            protected ConnectionsControl m_Owner;
            protected string m_Value = "";
            protected string m_Tooltip = "";

            public override void GetItemSearchString(string[] target, out int count)
            {
                count = 0;
                target[count++] = displayName;
                target[count++] = m_Value;
                target[count++] = string.Format(StringFormat.Address, address);
            }

            public override void OnGUI(Rect position, int column)
            {
                switch ((Column)column)
                {
                    case Column.Type:
                        HeEditorGUI.TypeName(position, displayName, m_Tooltip);
                        break;

                    case Column.Name:
                        EditorGUI.LabelField(position, m_Value);
                        break;

                    case Column.Address:
                        if (address != 0) // statics dont have an address in PackedMemorySnapshot and I don't want to display a misleading 0
                            HeEditorGUI.Address(position, address);
                        break;
                }
            }
        }

        // ------------------------------------------------------------------------

        class GCHandleItem : Item
        {
            PackedMemorySnapshot m_Snapshot;
            RichGCHandle m_GCHandle;

            public void Initialize(ConnectionsControl owner, PackedMemorySnapshot snapshot, int gcHandleArrayIndex)
            {
                m_Owner = owner;
                m_Snapshot = snapshot;
                m_GCHandle = new RichGCHandle(m_Snapshot, gcHandleArrayIndex);

                displayName = "GCHandle";
                m_Value = m_GCHandle.managedObject.isValid ? m_GCHandle.managedObject.type.name : "";
                address = m_GCHandle.managedObjectAddress;
            }

            public override void OnGUI(Rect position, int column)
            {
                if (column == 0)
                {
                    if (HeEditorGUI.GCHandleButton(HeEditorGUI.SpaceL(ref position, position.height)))
                    {
                        m_Owner.window.OnGoto(new GotoCommand(m_GCHandle));
                    }

                    if (m_GCHandle.nativeObject.isValid)
                    {
                        if (HeEditorGUI.CppButton(HeEditorGUI.SpaceR(ref position, position.height)))
                        {
                            m_Owner.window.OnGoto(new GotoCommand(m_GCHandle.nativeObject));
                        }
                    }

                    if (m_GCHandle.managedObject.isValid)
                    {
                        if (HeEditorGUI.CsButton(HeEditorGUI.SpaceR(ref position, position.height)))
                        {
                            m_Owner.window.OnGoto(new GotoCommand(m_GCHandle.managedObject));
                        }
                    }
                }

                base.OnGUI(position, column);
            }
        }

        // ------------------------------------------------------------------------

        class ManagedObjectItem : Item
        {
            RichManagedObject m_ManagedObject;

            public void Initialize(ConnectionsControl owner, PackedMemorySnapshot snapshot, int arrayIndex)
            {
                m_Owner = owner;
                m_ManagedObject = new RichManagedObject(snapshot, arrayIndex);

                displayName = m_ManagedObject.type.name;
                address = m_ManagedObject.address;
                m_Value = m_ManagedObject.nativeObject.isValid ? m_ManagedObject.nativeObject.name : "";
                m_Tooltip = PackedManagedTypeUtility.GetInheritanceAsString(snapshot, m_ManagedObject.type.packed.managedTypesArrayIndex);
            }

            public override void OnGUI(Rect position, int column)
            {
                if (column == 0)
                {
                    if (m_ManagedObject.gcHandle.isValid)
                    {
                        if (HeEditorGUI.GCHandleButton(HeEditorGUI.SpaceR(ref position, position.height)))
                        {
                            m_Owner.window.OnGoto(new GotoCommand(m_ManagedObject.gcHandle));
                        }
                    }

                    if (HeEditorGUI.CsButton(HeEditorGUI.SpaceL(ref position, position.height)))
                    {
                        m_Owner.window.OnGoto(new GotoCommand(m_ManagedObject));
                    }

                    if (m_ManagedObject.nativeObject.isValid)
                    {
                        if (HeEditorGUI.CppButton(HeEditorGUI.SpaceR(ref position, position.height)))
                        {
                            m_Owner.window.OnGoto(new GotoCommand(m_ManagedObject.nativeObject));
                        }
                    }
                }

                base.OnGUI(position, column);
            }
        }

        // ------------------------------------------------------------------------

        class ManagedStaticFieldItem : Item
        {
            PackedMemorySnapshot m_Snapshot;
            PackedManagedStaticField m_StaticField;

            public void Initialize(ConnectionsControl owner, PackedMemorySnapshot snapshot, int arrayIndex)
            {
                m_Owner = owner;
                m_Snapshot = snapshot;
                m_StaticField = m_Snapshot.managedStaticFields[arrayIndex];

                displayName = m_Snapshot.managedTypes[m_StaticField.managedTypesArrayIndex].name;
                address = 0;
                m_Value = "static " + m_Snapshot.managedTypes[m_StaticField.managedTypesArrayIndex].name + "." + m_Snapshot.managedTypes[m_StaticField.managedTypesArrayIndex].fields[m_StaticField.fieldIndex].name;
            }

            public override void OnGUI(Rect position, int column)
            {
                if (column == 0)
                {
                    if (HeEditorGUI.CsStaticButton(HeEditorGUI.SpaceL(ref position, position.height)))
                    {
                        m_Owner.window.OnGoto(new GotoCommand(new RichStaticField(m_Snapshot, m_StaticField.staticFieldsArrayIndex)));
                    }
                }

                base.OnGUI(position, column);
            }
        }

        // ------------------------------------------------------------------------

        class NativeObjectItem : Item
        {
            PackedMemorySnapshot m_Snapshot;
            RichNativeObject m_NativeObject;

            public void Initialize(ConnectionsControl owner, PackedMemorySnapshot snapshot, PackedNativeUnityEngineObject nativeObject)
            {
                m_Owner = owner;
                m_Snapshot = snapshot;
                m_NativeObject = new RichNativeObject(snapshot, nativeObject.nativeObjectsArrayIndex);

                m_Value = m_NativeObject.name;
                address = m_NativeObject.address;
                displayName = m_NativeObject.type.name;

                // If it's a MonoBehaviour or ScriptableObject, use the C# typename instead
                // It makes it easier to understand what it is, otherwise everything displays 'MonoBehaviour' only.
                if (m_NativeObject.type.IsSubclassOf(m_Snapshot.coreTypes.nativeMonoBehaviour) || m_NativeObject.type.IsSubclassOf(m_Snapshot.coreTypes.nativeScriptableObject))
                {
                    string monoScriptName;
                    if (m_Snapshot.FindNativeMonoScriptType(m_NativeObject.packed.nativeObjectsArrayIndex, out monoScriptName) != -1)
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
                        m_Owner.window.OnGoto(new GotoCommand(m_NativeObject));
                    }

                    if (m_NativeObject.gcHandle.isValid)
                    {
                        if (HeEditorGUI.GCHandleButton(HeEditorGUI.SpaceR(ref position, position.height)))
                        {
                            m_Owner.window.OnGoto(new GotoCommand(m_NativeObject.gcHandle));
                        }
                    }

                    if (m_NativeObject.managedObject.isValid)
                    {
                        if (HeEditorGUI.CsButton(HeEditorGUI.SpaceR(ref position, position.height)))
                        {
                            m_Owner.window.OnGoto(new GotoCommand(m_NativeObject.managedObject));
                        }
                    }
                }

                base.OnGUI(position, column);
            }
        }
    }
}
