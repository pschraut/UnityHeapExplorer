//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//
//#define HEAPEXPLORER_DISPLAY_REFS

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;
using HeapExplorer.Utilities;
using static HeapExplorer.Utilities.Option;

namespace HeapExplorer
{
    public class NativeObjectsControl : AbstractTreeView
    {
        public System.Action<Option<PackedNativeUnityEngineObject>> onSelectionChange;

        public long nativeObjectsCount
        {
            get
            {
                return m_NativeObjectsCount;
            }
        }

        public ulong nativeObjectsSize
        {
            get
            {
                return m_NativeObjectsSize;
            }
        }

        protected long m_NativeObjectsCount;
        protected ulong m_NativeObjectsSize;

        PackedMemorySnapshot m_Snapshot;
        int m_UniqueId = 1;

        enum Column
        {
            Type,
            Name,
            Size,
            Count,
            DontDestroyOnLoad,
            IsPersistent,
            Address,
            InstanceID,

#if HEAPEXPLORER_DISPLAY_REFS
            ReferencesCount,
            ReferencedByCount
#endif
        }

        public NativeObjectsControl(HeapExplorerWindow window, string editorPrefsKey, TreeViewState state)
            : base(window, editorPrefsKey, state, new MultiColumnHeader(
                new MultiColumnHeaderState(new[]
                {
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Type"), width = 250, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Name"), width = 250, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Size"), width = 80, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Count"), width = 50, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("DDoL", "Don't Destroy on Load\nHas this object has been marked as DontDestroyOnLoad?"), width = 50, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Persistent", "Is this object persistent?\nAssets are persistent, objects stored in scenes are persistent, dynamically created objects are not."), width = 50, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Address"), width = 120, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("InstanceID", "InstanceID"), width = 120, autoResize = true },
#if HEAPEXPLORER_DISPLAY_REFS
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Refs", "Refereces Count"), width = 50, autoResize = true },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("RefBy", "ReferencedBy Count"), width = 50, autoResize = true },
#endif
                })))
        {
            multiColumnHeader.canSort = true;

            Reload();
        }

        public void Select(PackedNativeUnityEngineObject obj)
        {
            var item = FindItemByAddressRecursive(rootItem, obj.nativeObjectAddress);
            SelectItem(item);
        }

        TreeViewItem FindItemByAddressRecursive(TreeViewItem parent, System.UInt64 address)
        {
            if (parent != null)
            {
                var item = parent as AbstractItem;
                if (item != null && item.address == address)
                    return item;

                if (parent.hasChildren)
                {
                    for (int n = 0, nend = parent.children.Count; n < nend; ++n)
                    {
                        var child = parent.children[n];

                        var value = FindItemByAddressRecursive(child, address);
                        if (value != null)
                            return value;
                    }
                }
            }

            return null;
        }

        protected override void OnSelectionChanged(TreeViewItem selectedItem)
        {
            base.OnSelectionChanged(selectedItem);

            if (onSelectionChange == null)
                return;

            var item = selectedItem as NativeObjectItem;
            if (item == null)
            {
                onSelectionChange.Invoke(None._);
                return;
            }

            onSelectionChange.Invoke(Some(item.packed));
        }

        public struct BuildArgs
        {
            public bool addAssetObjects;
            public bool addSceneObjects;
            public bool addRuntimeObjects;
            public bool addDestroyOnLoad;
            public bool addDontDestroyOnLoad;

            public bool CanAdd(PackedNativeUnityEngineObject no)
            {
                if (!addAssetObjects && no.isPersistent) return false;
                if (!addSceneObjects && !no.isPersistent && no.instanceId >= 0) return false;
                if (!addRuntimeObjects && !no.isPersistent && no.instanceId < 0) return false;

                var dontDestroy = false;
                if (no.isDontDestroyOnLoad || no.isManager || ((no.hideFlags & HideFlags.DontUnloadUnusedAsset) != 0))
                    dontDestroy = true;

                if (!addDestroyOnLoad && !dontDestroy) return false;
                if (!addDontDestroyOnLoad && dontDestroy) return false;

                return true;
            }
        }

        public TreeViewItem BuildTree(PackedMemorySnapshot snapshot, BuildArgs buildArgs)
        {
            m_Snapshot = snapshot;
            m_UniqueId = 1;
            m_NativeObjectsCount = 0;
            m_NativeObjectsSize = 0;

            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
            if (m_Snapshot == null)
            {
                root.AddChild(new TreeViewItem { id = 1, depth = -1, displayName = "" });
                return root;
            }

            // int=typeIndex
            var groupLookup = new Dictionary<long, GroupItem>();

            for (int n = 0, nend = m_Snapshot.nativeObjects.Length; n < nend; ++n)
            {
                if (window.isClosing) // the window is closing
                    break;

                var no = m_Snapshot.nativeObjects[n];
                if (!buildArgs.CanAdd(no))
                    continue;

                GroupItem group;
                if (!groupLookup.TryGetValue(no.nativeTypesArrayIndex, out group))
                {
                    group = new GroupItem
                    {
                        id = m_UniqueId++,
                        depth = root.depth + 1,
                        displayName = ""
                    };
                    group.Initialize(m_Snapshot, no.nativeTypesArrayIndex);

                    groupLookup[no.nativeTypesArrayIndex] = group;
                    root.AddChild(group);
                }

                var typeNameOverride = "";
                // Derived MonoBehaviour types appear just as MonoBehaviour on the native side.
                // This is not very informative. However, the actual name can be derived from the MonoScript of such MonoBehaviour instead.
                // The following tries to find the corresponding MonoScript and uses the MonoScript name instead.
                #region Add MonoBehaviour using name of MonoScript
                if (no.nativeTypesArrayIndex == m_Snapshot.coreTypes.nativeMonoBehaviour ||
                    no.nativeTypesArrayIndex == m_Snapshot.coreTypes.nativeScriptableObject)
                {
                    var maybeMonoScript = m_Snapshot.FindNativeMonoScriptType(no.nativeObjectsArrayIndex);
                    if (maybeMonoScript.valueOut(out var monoScript) && monoScript.index < m_Snapshot.nativeTypes.Length)
                    {
                        typeNameOverride = monoScript.monoScriptName;
                        long key = (monoScript.monoScriptName.GetHashCode() << 32) | monoScript.index;

                        GroupItem group2;
                        if (!groupLookup.TryGetValue(key, out group2))
                        {
                            group2 = new GroupItem
                            {
                                id = m_UniqueId++,
                                depth = group.depth + 1,
                                //displayName = monoScriptName,
                                typeNameOverride = monoScript.monoScriptName,
                            };
                            group2.Initialize(m_Snapshot, no.nativeTypesArrayIndex);

                            groupLookup[key] = group2;
                            group.AddChild(group2);
                        }
                        group = group2;
                    }
                }
                #endregion

                var item = new NativeObjectItem
                {
                    id = m_UniqueId++,
                    depth = group.depth + 1,
                    displayName = "",
                    typeNameOverride = typeNameOverride
                };
                item.Initialize(this, no);

                m_NativeObjectsCount++;
                m_NativeObjectsSize += item.size;

                group.AddChild(item);
            }

            // remove groups if it contains one item only
            if (root.hasChildren)
            {
                for (int n = root.children.Count - 1; n >= 0; --n)
                {
                    var group = root.children[n];
                    if (group.children.Count == 1)
                    {
                        group.children[0].depth -= 1;
                        root.AddChild(group.children[0]);
                        root.children.RemoveAt(n);
                    }
                }
            }

            if (!root.hasChildren)
                root.AddChild(new TreeViewItem { id = 1, depth = -1, displayName = "" });

            return root;
        }

        public TreeViewItem BuildDuplicatesTree(PackedMemorySnapshot snapshot, BuildArgs buildArgs)
        {
            m_Snapshot = snapshot;
            m_UniqueId = 1;
            m_NativeObjectsCount = 0;
            m_NativeObjectsSize = 0;

            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
            if (m_Snapshot == null)
            {
                root.AddChild(new TreeViewItem { id = 1, depth = -1, displayName = "" });
                return root;
            }

            var dupesLookup = new Dictionary<Hash128, List<int>>();

            for (int n = 0, nend = m_Snapshot.nativeObjects.Length; n < nend; ++n)
            {
                if (window.isClosing) // the window is closing
                    break;

                var no = m_Snapshot.nativeObjects[n];
                if (!no.isPersistent)
                    continue;

                if (!buildArgs.CanAdd(no))
                    continue;

                if (no.nativeTypesArrayIndex == -1)
                    continue;

                var nativeType = m_Snapshot.nativeTypes[no.nativeTypesArrayIndex];
                if (!nativeType.managedTypeArrayIndex.valueOut(out var managedTypeArrayIndex))
                    continue;

                if (m_Snapshot.IsSubclassOf(m_Snapshot.managedTypes[managedTypeArrayIndex], m_Snapshot.coreTypes.unityEngineComponent))
                    continue;

                if (m_Snapshot.IsSubclassOf(m_Snapshot.managedTypes[managedTypeArrayIndex], m_Snapshot.coreTypes.unityEngineGameObject))
                    continue;

                var hash = new Hash128((uint)no.nativeTypesArrayIndex, (uint)no.size, (uint)no.name.GetHashCode(), 0);

                List<int> list;
                if (!dupesLookup.TryGetValue(hash, out list))
                    dupesLookup[hash] = list = new List<int>();

                list.Add(n);
            }

            // int=typeIndex
            var groupLookup = new Dictionary<Hash128, GroupItem>();

            foreach(var dupePair in dupesLookup)
            for (int n = 0, nend = dupePair.Value.Count; n < nend; ++n)
            {
                if (window.isClosing) // the window is closing
                    break;

                var list = dupePair.Value;
                if (list.Count <= 1)
                    continue;

                var no = m_Snapshot.nativeObjects[list[n]];

                GroupItem group;
                if (!groupLookup.TryGetValue(dupePair.Key, out group))
                {
                    group = new GroupItem
                    {
                        id = m_UniqueId++,
                        depth = root.depth + 1,
                        displayName = no.name
                    };
                    group.Initialize(m_Snapshot, no.nativeTypesArrayIndex);

                    groupLookup[dupePair.Key] = group;
                    root.AddChild(group);
                }

                var item = new NativeObjectItem
                {
                    id = m_UniqueId++,
                    depth = group.depth + 1,
                    displayName = ""
                };
                item.Initialize(this, no);

                m_NativeObjectsCount++;
                m_NativeObjectsSize += item.size;

                group.AddChild(item);
            }

            if (!root.hasChildren)
                root.AddChild(new TreeViewItem { id = 1, depth = -1, displayName = "" });

            return root;
        }

        protected override int OnSortItem(TreeViewItem aa, TreeViewItem bb)
        {
            var sortingColumn = multiColumnHeader.sortedColumnIndex;
            var ascending = multiColumnHeader.IsSortedAscending(sortingColumn);

            var itemA = (ascending ? aa : bb) as AbstractItem;
            var itemB = (ascending ? bb : aa) as AbstractItem;

            switch ((Column)sortingColumn)
            {
                case Column.Name:
                    return string.Compare(itemB.name ?? "", itemA.name ?? "", StringComparison.OrdinalIgnoreCase);

                case Column.Type:
                    return string.Compare(itemB.typeName, itemA.typeName, StringComparison.OrdinalIgnoreCase);

                case Column.Size:
                    return itemA.size.CompareTo(itemB.size);

                case Column.Count:
                    return itemA.count.CompareTo(itemB.count);

                case Column.Address:
                    return itemA.address.CompareTo(itemB.address);

                case Column.DontDestroyOnLoad:
                    return itemA.isDontDestroyOnLoad.CompareTo(itemB.isDontDestroyOnLoad);

                case Column.IsPersistent:
                    return itemA.isPersistent.CompareTo(itemB.isPersistent);

                case Column.InstanceID:
                    return itemA.instanceId.CompareTo(itemB.instanceId);

#if HEAPEXPLORER_DISPLAY_REFS
                case Column.ReferencesCount:
                    return itemA.referencesCount.CompareTo(itemB.referencesCount);

                case Column.ReferencedByCount:
                    return itemA.referencedByCount.CompareTo(itemB.referencedByCount);
#endif
            }

            return 0;
        }

        ///////////////////////////////////////////////////////////////////////////
        // AbstractItem
        ///////////////////////////////////////////////////////////////////////////

        abstract class AbstractItem : AbstractTreeViewItem
        {
            public string typeNameOverride;

            public abstract string typeName { get; }
            public abstract string name { get; }
            public abstract ulong size { get; }
            public abstract int count { get; }
            public abstract System.UInt64 address { get; }
            public abstract bool isDontDestroyOnLoad { get; }
            public abstract bool isPersistent { get; }
            public abstract int instanceId { get; }
#if HEAPEXPLORER_DISPLAY_REFS
            public abstract int referencesCount { get; }
            public abstract int referencedByCount { get; }
#endif
        }

        ///////////////////////////////////////////////////////////////////////////
        // NativeObjectItem
        ///////////////////////////////////////////////////////////////////////////

        class NativeObjectItem : AbstractItem
        {
            NativeObjectsControl m_Owner;
            RichNativeObject m_Object;
#if HEAPEXPLORER_DISPLAY_REFS
            int m_ReferencesCount;
            int m_ReferencedByCount;
#endif

            public PackedNativeUnityEngineObject packed
            {
                get
                {
                    return m_Object.packed;
                }
            }

#if HEAPEXPLORER_DISPLAY_REFS
            public override int referencesCount
            {
                get
                {
                    return m_ReferencesCount;
                }
            }

            public override int referencedByCount
            {
                get
                {
                    return m_ReferencedByCount;
                }
            }
#endif

            public override string typeName
            {
                get
                {
                    if (typeNameOverride != null && typeNameOverride.Length > 0)
                        return typeNameOverride;

                    return m_Object.type.name;
                }
            }

            public override string name
            {
                get
                {
                    return m_Object.name;
                }
            }

            public override ulong size
            {
                get
                {
                    return m_Object.size;
                }
            }

            public override int count
            {
                get
                {
                    return 0;
                }
            }

            public override System.UInt64 address
            {
                get
                {
                    return m_Object.packed.nativeObjectAddress;
                }
            }

            public override bool isDontDestroyOnLoad
            {
                get
                {
                    return m_Object.isDontDestroyOnLoad;
                }
            }

            public override bool isPersistent
            {
                get
                {
                    return m_Object.isPersistent;
                }
            }

            public override int instanceId
            {
                get
                {
                    return m_Object.instanceId;
                }
            }

            public void Initialize(NativeObjectsControl owner, PackedNativeUnityEngineObject nativeObject)
            {
                m_Owner = owner;
                m_Object = new RichNativeObject(owner.m_Snapshot, nativeObject.nativeObjectsArrayIndex);
#if HEAPEXPLORER_DISPLAY_REFS
                m_Object.GetConnectionsCount(out m_ReferencesCount, out m_ReferencedByCount);
#endif
            }

            public override void GetItemSearchString(string[] target, out int count, out string type, out string label)
            {
                base.GetItemSearchString(target, out count, out type, out label);

                type = m_Object.type.name;
                target[count++] = m_Object.name;
                target[count++] = m_Object.type.name;
                target[count++] = string.Format(StringFormat.Address, address);
                target[count++] = instanceId.ToString();
            }

            public override void OnGUI(Rect position, int column)
            {
                if (column == 0)
                {
                    HeEditorGUI.NativeObjectIcon(HeEditorGUI.SpaceL(ref position, position.height), m_Object.packed);

                    if (m_Object.managedObject.valueOut(out var managedObject))
                    {
                        if (HeEditorGUI.CsButton(HeEditorGUI.SpaceR(ref position, position.height)))
                        {
                            m_Owner.window.OnGoto(new GotoCommand(managedObject));
                        }
                    }
                }

                switch ((Column)column)
                {
                    case Column.Type:
                        GUI.Label(position, typeName);
                        break;

                    case Column.Name:
                        {
                            GUI.Label(position, name);

                            var e = Event.current;
                            if (e.type == EventType.ContextClick && position.Contains(e.mousePosition))
                            {
                                var menu = new GenericMenu();
                                OnShowNameContextMenu(menu);
                                if (menu.GetItemCount() > 0)
                                {
                                    e.Use();
                                    menu.ShowAsContext();
                                }
                            }
                        }
                        break;

                    case Column.Size:
                        HeEditorGUI.Size(position, size.ToLongClamped());
                        break;

                    case Column.Address:
                        HeEditorGUI.Address(position, address);
                        break;

                    case Column.DontDestroyOnLoad:
                        GUI.Label(position, isDontDestroyOnLoad.ToString());
                        break;

                    case Column.IsPersistent:
                        GUI.Label(position, isPersistent.ToString());
                        break;

                    case Column.InstanceID:
                        GUI.Label(position, instanceId.ToString());
                        break;

#if HEAPEXPLORER_DISPLAY_REFS
                    case Column.ReferencesCount:
                        GUI.Label(position, m_ReferencesCount.ToString());
                        break;

                    case Column.ReferencedByCount:
                        GUI.Label(position, m_ReferencedByCount.ToString());
                        break;
#endif
                }
            }

            void OnShowNameContextMenu(GenericMenu menu)
            {
                if (!string.IsNullOrEmpty(m_Object.name))
                {
                    menu.AddItem(new GUIContent("Find in Project"), false, (GenericMenu.MenuFunction2)delegate (object userData)
                    {
                        var o = (RichNativeObject)userData;
                        HeEditorUtility.SearchProjectBrowser($"t:{o.type.name} {o.name}");
                    }, m_Object);
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        // GroupItem
        ///////////////////////////////////////////////////////////////////////////

        class GroupItem : AbstractItem
        {
            RichNativeType m_Type;

#if HEAPEXPLORER_DISPLAY_REFS
            int m_ReferencesCount = -1;
            public override int referencesCount
            {
                get
                {
                    if (m_ReferencesCount == -1)
                    {
                        m_ReferencesCount = 0;
                        if (hasChildren)
                        {
                            for (int n = 0, nend = children.Count; n < nend; ++n)
                            {
                                var child = children[n] as AbstractItem;
                                if (child != null)
                                {
                                    var count = child.referencesCount;
                                    if (count > m_ReferencesCount)
                                        m_ReferencesCount = count;
                                }
                            }
                        }
                    }

                    return m_ReferencesCount;
                }
            }

            int m_ReferencedByCount = -1;
            public override int referencedByCount
            {
                get
                {
                    if (m_ReferencedByCount == -1)
                    {
                        m_ReferencedByCount = 0;
                        if (hasChildren)
                        {
                            for (int n = 0, nend = children.Count; n < nend; ++n)
                            {
                                var child = children[n] as AbstractItem;
                                if (child != null)
                                {
                                    var count = child.referencedByCount;
                                    if (count > m_ReferencedByCount)
                                        m_ReferencedByCount = count;
                                }
                            }
                        }
                    }

                    return m_ReferencedByCount;
                }
            }
#endif

            public override string typeName
            {
                get
                {
                    if (typeNameOverride != null && typeNameOverride.Length > 0)
                        return typeNameOverride;

                    return m_Type.name;
                }
            }

            public override string name
            {
                get
                {
                    return displayName ?? "";
                }
            }

            long m_Size = -1;
            public override ulong size
            {
                get
                {
                    if (m_Size == -1)
                    {
                        m_Size = 0;
                        if (hasChildren)
                        {
                            for (int n = 0, nend = children.Count; n < nend; ++n)
                            {
                                var child = children[n] as AbstractItem;
                                if (child != null)
                                    m_Size += child.size.ToLongClamped();
                            }
                        }
                    }

                    return m_Size.ToULongClamped();
                }
            }

            int m_Count = -1;
            public override int count
            {
                get
                {
                    if (m_Count == -1)
                    {
                        m_Count = 0;
                        if (hasChildren)
                        {
                            m_Count += children.Count;
                            for (int n = 0, nend = children.Count; n < nend; ++n)
                            {
                                var child = children[n] as AbstractItem;
                                if (child != null)
                                    m_Count += child.count;
                            }
                        }
                    }

                    return m_Count;
                }
            }

            public override System.UInt64 address
            {
                get
                {
                    return 0;
                }
            }

            public override bool isDontDestroyOnLoad
            {
                get
                {
                    return false;
                }
            }

            public override bool isPersistent
            {
                get
                {
                    return false;
                }
            }

            public override int instanceId
            {
                get
                {
                    return 0;
                }
            }

            public void Initialize(PackedMemorySnapshot snapshot, PInt managedTypeArrayIndex)
            {
                m_Type = new RichNativeType(snapshot, managedTypeArrayIndex);
            }

            public override void OnGUI(Rect position, int column)
            {
                switch ((Column)column)
                {
                    case Column.Type:
                        HeEditorGUI.TypeName(position, typeName);
                        break;

                    case Column.Name:
                        HeEditorGUI.TypeName(position, name);
                        break;

                    case Column.Size:
                        HeEditorGUI.Size(position, size.ToLongClamped());
                        break;

                    case Column.Count:
                        GUI.Label(position, count.ToString());
                        break;
                }
            }
        }
    }
}
