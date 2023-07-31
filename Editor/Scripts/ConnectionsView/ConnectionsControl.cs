//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//

using System;
using System.Collections.Generic;
using HeapExplorer.Utilities;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;
using static HeapExplorer.Utilities.Option;
using static HeapExplorer.ViewConsts;

namespace HeapExplorer
{
    public class GotoCommand
    {
        public HeapExplorerView fromView;
        public HeapExplorerView toView;
        
        public Option<RichGCHandle> toGCHandle;
        public Option<RichManagedObject> toManagedObject;
        public Option<RichNativeObject> toNativeObject;
        public Option<RichStaticField> toStaticField;
        public Option<RichManagedType> toManagedType;

        public GotoCommand()
        {
        }

        public GotoCommand(RichGCHandle value)
            : this()
        {
            toGCHandle = Some(value);
        }

        public GotoCommand(RichManagedObject value)
            : this()
        {
            toManagedObject = Some(value);
        }

        public GotoCommand(RichNativeObject value)
            : this()
        {
            toNativeObject = Some(value);
        }

        public GotoCommand(RichStaticField value)
            : this()
        {
            toStaticField = Some(value);
        }

        public GotoCommand(RichManagedType value)
            : this()
        {
            toManagedType = Some(value);
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
        
        int m_UniqueId = 1;

        enum Column
        {
            Type,
            Name,
            SourceField,
            Address,
        }

        public ConnectionsControl(HeapExplorerWindow window, string editorPrefsKey, TreeViewState state)
            : base(window, editorPrefsKey, state, new MultiColumnHeader(
                new MultiColumnHeaderState(new[] {
                    new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Type"), width = 200, autoResize = true },
                    new MultiColumnHeaderState.Column() { headerContent = new GUIContent(COLUMN_CPP_NAME, COLUMN_CPP_NAME_DESCRIPTION), width = 200, autoResize = true },
                    new MultiColumnHeaderState.Column() { headerContent = new GUIContent(COLUMN_SOURCE_FIELD, COLUMN_SOURCE_FIELD_DESCRIPTION), width = 200, autoResize = true },
                    new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Address"), width = 200, autoResize = true },
                })))
        {
            extraSpaceBeforeIconAndLabel = 4;
            columnIndexForTreeFoldouts = 0;
            multiColumnHeader.canSort = false;

            Reload();
        }

        /// <summary>
        /// The <see cref="connections"/> here are stored as <see cref="PackedConnection.From"/> because this component
        /// is reused for both `from` and `to` connections and `from` connections contain more data.
        /// <para/>
        /// The `to` connections will be converted to <see cref="PackedConnection.From"/> with
        /// <see cref="<see cref="PackedConnection.From.field"/> missing.
        /// </summary>
        public TreeViewItem BuildTree(PackedMemorySnapshot snapshot, PackedConnection.From[] connections) {
            m_Snapshot = snapshot;
            m_UniqueId = 1;

            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
            if (m_Snapshot == null || connections == null || connections.Length < 1) {
                root.AddChild(new TreeViewItem { id = 1, depth = -1, displayName = "" });
                return root;
            }

            for (int n = 0, nend = connections.Length; n < nend; ++n)
            {
                if (window.isClosing) // the window is closing
                    break;

                var connection = connections[n];

                switch (connection.pair.kind) {
                    case PackedConnection.Kind.GCHandle:
                        AddGCHandle(root, m_Snapshot.gcHandles[connection.pair.index], connection.field);
                        break;

                    case PackedConnection.Kind.Managed:
                        AddManagedObject(root, m_Snapshot.managedObjects[connection.pair.index], connection.field);
                        break;

                    case PackedConnection.Kind.Native:
                        AddNativeUnityObject(root, m_Snapshot.nativeObjects[connection.pair.index], connection.field);
                        break;

                    case PackedConnection.Kind.StaticField:
                        AddStaticField(root, m_Snapshot.managedStaticFields[connection.pair.index], connection.field);
                        break;
                    
                    default:
                        throw new ArgumentOutOfRangeException(nameof(connection.pair.kind), connection.pair.kind, "unknown kind");
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

        void AddGCHandle(TreeViewItem parent, PackedGCHandle gcHandle, Option<PackedManagedField> field)
        {
            var item = new GCHandleItem
            {
                id = m_UniqueId++,
                depth = parent.depth + 1,
                fieldName = field.fold("", _ => _.name) 
            };

            item.Initialize(this, m_Snapshot, gcHandle.gcHandlesArrayIndex);
            parent.AddChild(item);
        }

        void AddManagedObject(
            TreeViewItem parent, PackedManagedObject managedObject, Option<PackedManagedField> field
        ) {
            var item = new ManagedObjectItem
            {
                id = m_UniqueId++,
                depth = parent.depth + 1,
                fieldName = field.fold("", _ => _.name) 
            };

            item.Initialize(this, m_Snapshot, managedObject.managedObjectsArrayIndex);
            parent.AddChild(item);
        }

        void AddNativeUnityObject(
            TreeViewItem parent, PackedNativeUnityEngineObject nativeObject, Option<PackedManagedField> field
        ) {
            var item = new NativeObjectItem
            {
                id = m_UniqueId++,
                depth = parent.depth + 1,
                fieldName = field.fold("", _ => _.name) 
            };

            item.Initialize(this, m_Snapshot, nativeObject);
            parent.AddChild(item);
        }

        void AddStaticField(
            TreeViewItem parent, PackedManagedStaticField staticField, Option<PackedManagedField> field
        ) {
            var item = new ManagedStaticFieldItem
            {
                id = m_UniqueId++,
                depth = parent.depth + 1,
                fieldName = field.fold("", _ => _.name) 
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
            
            /// <summary>Name of the field if it makes sense.</summary>
            public string fieldName = "";
            
            protected string m_Tooltip = "";

            public override void GetItemSearchString(string[] target, out int count, out string type, out string label)
            {
                base.GetItemSearchString(target, out count, out type, out label);

                type = displayName;
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

                    case Column.SourceField:
                        EditorGUI.LabelField(position, fieldName);
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
                m_Value = m_GCHandle.managedObject.fold("", _ => _.type.name);
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

                    {if (m_GCHandle.nativeObject.valueOut(out var nativeObject)) {
                        if (HeEditorGUI.CppButton(HeEditorGUI.SpaceR(ref position, position.height)))
                        {
                            m_Owner.window.OnGoto(new GotoCommand(nativeObject));
                        }
                    }}

                    {if (m_GCHandle.managedObject.valueOut(out var managedObject)) {
                        if (HeEditorGUI.CsButton(HeEditorGUI.SpaceR(ref position, position.height)))
                        {
                            m_Owner.window.OnGoto(new GotoCommand(managedObject));
                        }
                    }}
                }

                base.OnGUI(position, column);
            }
        }

        // ------------------------------------------------------------------------

        class ManagedObjectItem : Item
        {
            RichManagedObject m_ManagedObject;

            public void Initialize(
                ConnectionsControl owner, PackedMemorySnapshot snapshot, PackedManagedObject.ArrayIndex arrayIndex
            ) {
                m_Owner = owner;
                m_ManagedObject = new RichManagedObject(snapshot, arrayIndex);

                displayName = m_ManagedObject.type.name;
                address = m_ManagedObject.address;
                m_Value = m_ManagedObject.nativeObject.fold("", _ => _.name);
                m_Tooltip = PackedManagedTypeUtility.GetInheritanceAsString(snapshot, m_ManagedObject.type.packed.managedTypesArrayIndex);
            }

            public override void OnGUI(Rect position, int column)
            {
                if (column == 0)
                {
                    {if (m_ManagedObject.gcHandle.valueOut(out var gcHandle)) {
                        if (HeEditorGUI.GCHandleButton(HeEditorGUI.SpaceR(ref position, position.height)))
                        {
                            m_Owner.window.OnGoto(new GotoCommand(gcHandle));
                        }
                    }}

                    if (HeEditorGUI.CsButton(HeEditorGUI.SpaceL(ref position, position.height)))
                    {
                        m_Owner.window.OnGoto(new GotoCommand(m_ManagedObject));
                    }

                    {if (m_ManagedObject.nativeObject.valueOut(out var nativeObject)) {
                        if (HeEditorGUI.CppButton(HeEditorGUI.SpaceR(ref position, position.height)))
                        {
                            m_Owner.window.OnGoto(new GotoCommand(nativeObject));
                        }
                    }}
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
                    if (m_Snapshot.FindNativeMonoScriptType(m_NativeObject.packed.nativeObjectsArrayIndex).valueOut(out var tpl))
                    {
                        if (!string.IsNullOrEmpty(tpl.monoScriptName))
                            displayName = tpl.monoScriptName;
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

                    {if (m_NativeObject.gcHandle.valueOut(out var gcHandle)) {
                        if (HeEditorGUI.GCHandleButton(HeEditorGUI.SpaceR(ref position, position.height)))
                        {
                            m_Owner.window.OnGoto(new GotoCommand(gcHandle));
                        }
                    }}

                    {if (m_NativeObject.managedObject.valueOut(out var managedObject)) {
                        if (HeEditorGUI.CsButton(HeEditorGUI.SpaceR(ref position, position.height)))
                        {
                            m_Owner.window.OnGoto(new GotoCommand(managedObject));
                        }
                    }}
                }

                base.OnGUI(position, column);
            }
        }
    }
}
