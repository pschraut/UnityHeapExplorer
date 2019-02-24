//
// Heap Explorer for Unity. Copyright (c) 2019 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://bitbucket.org/pschraut/unityheapexplorer/
//
using System.Collections;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace HeapExplorer
{
    public class Progress
    {
        public float value;
        public string info = "";
    }

    public class ManagedObjectDuplicatesControl : AbstractManagedObjectsControl
    {
        public Progress progress = new Progress();

        public ManagedObjectDuplicatesControl(HeapExplorerWindow window, string editorPrefsKey, TreeViewState state)
            : base(window, editorPrefsKey, state)
        {
        }

        protected override void OnBuildTree(TreeViewItem root)
        {
            progress.value = 0;

            var lookup = new Dictionary<Hash128, AbstractItem>();
            var memoryReader = new MemoryReader(m_Snapshot);

            for (int n = 0, nend = m_Snapshot.managedObjects.Length; n < nend; ++n)
            {
                progress.value = (n + 1.0f) / nend;

                var obj = m_Snapshot.managedObjects[n];
                if (obj.address == 0)
                    continue;

                var type = m_Snapshot.managedTypes[obj.managedTypesArrayIndex];
                if (type.isPrimitive && !type.isPointer)
                    continue;

                if (type.isValueType)
                    continue;

                var hash = memoryReader.ComputeObjectHash(obj.address, type);

                AbstractItem parent;
                if (!lookup.TryGetValue(hash, out parent))
                {
                    var group = new GroupItem()
                    {
                        id = m_UniqueId++,
                        depth = root.depth + 1,
                        displayName = ""
                    };
                    group.Initialize(m_Snapshot, type);

                    lookup[hash] = parent = group;
                    root.AddChild(group);
                }

                var item = new ManagedObjectItem
                {
                    id = m_UniqueId++,
                    depth = parent.depth + 1,
                    displayName = ""
                };
                item.Initialize(this, m_Snapshot, obj);
                parent.AddChild(item);
            }

            if (root.hasChildren)
            {
                for (var n = root.children.Count - 1; n >= 0; --n)
                {
                    if (!root.children[n].hasChildren)
                    {
                        root.children.RemoveAt(n);
                        continue;
                    }

                    if (root.children[n].children.Count < 2)
                    {
                        root.children.RemoveAt(n);
                        continue;
                    }

                    var item = root.children[n] as AbstractItem;
                    m_ManagedObjectCount += item.count;
                    m_ManagedObjectSize += item.size;
                }
            }

            progress.value = 1;
        }
    }
}
