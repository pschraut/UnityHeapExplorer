//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//
using System.Collections;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace HeapExplorer
{
    public class ManagedEmptyShellObjectsControl : AbstractManagedObjectsControl
    {
        public Progress progress = new Progress();

        public ManagedEmptyShellObjectsControl(HeapExplorerWindow window, string editorPrefsKey, TreeViewState state)
            : base(window, editorPrefsKey, state)
        {
        }

        protected override void OnBuildTree(TreeViewItem root)
        {
            progress.value = 0;

            var lookup = new Dictionary<string, AbstractItem>();
            var memoryReader = new MemoryReader(m_Snapshot);

            for (int n = 0, nend = m_Snapshot.managedObjects.Length; n < nend; ++n)
            {
                progress.value = (n + 1.0f) / nend;

                var obj = m_Snapshot.managedObjects[n];
                if (obj.address == 0)
                    continue; // points to null

                if (obj.nativeObjectsArrayIndex.isSome)
                    continue; // has a native object, thus can't be an empty shell object

                var type = m_Snapshot.managedTypes[obj.managedTypesArrayIndex];

                // Only UnityEngine.Object objects can have a m_CachedPtr connection to a native object.
                if (!type.isUnityEngineObject)
                    continue;

                // Could be an array of an UnityEngine.Object, such as Texture[]
                if (type.isArray)
                    continue;

                // Get type as a "higher level" representation that is easier to work with
                var richType = new RichManagedType(m_Snapshot, obj.managedTypesArrayIndex);

                // Try to get the m_InstanceID field (only exists in editor, not in built players)
                if (richType.FindField("m_InstanceID", out var packedField)) {
                    var instanceIDPtr = obj.address + packedField.offset;
                    if (!memoryReader.ReadInt32(instanceIDPtr).valueOut(out var instanceID)) {
                        m_Snapshot.Error($"Can't read 'instanceID' from address {instanceIDPtr:X}, skipping."); 
                        continue;
                    }
                    // The editor contains various empty shell objects whose instanceID all contain 0.
                    // I guess it's some kind of special object? In this case we just ignore them.
                    if (instanceID == 0)
                        continue;
                }

                // Check if we already have a grouping node for that type.
                // Create a new node if we don't have it.
                AbstractItem parent;
                if (!lookup.TryGetValue(type.name, out parent))
                {
                    var group = new GroupItem()
                    {
                        id = m_UniqueId++,
                        depth = root.depth + 1,
                        displayName = ""
                    };
                    group.Initialize(m_Snapshot, type);

                    lookup[type.name] = parent = group;
                    root.AddChild(group);
                }

                // Create and add the managed object item
                var item = new ManagedObjectItem
                {
                    id = m_UniqueId++,
                    depth = parent.depth + 1,
                    displayName = ""
                };
                item.Initialize(this, m_Snapshot, obj);
                parent.AddChild(item);

                m_ManagedObjectCount++;
                m_ManagedObjectSize += item.size;
            }

            progress.value = 1;
        }
    }
}
