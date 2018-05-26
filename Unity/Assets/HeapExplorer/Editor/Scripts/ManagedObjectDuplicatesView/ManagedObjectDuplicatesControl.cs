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

        public ManagedObjectDuplicatesControl(string editorPrefsKey, TreeViewState state)
            : base(editorPrefsKey, state)
        {
        }

        protected override void OnBuildTree(TreeViewItem root)
        {
            progress.value = 0;

            var lookup = new Dictionary<Hash128, AbstractItem>();
            var memoryReader = new MemoryReader(m_snapshot);

            for (int n = 0, nend = m_snapshot.managedObjects.Length; n < nend; ++n)
            {
                progress.value = (n + 1.0f) / nend;

                var obj = m_snapshot.managedObjects[n];
                if (obj.address == 0)
                    continue;

                var type = m_snapshot.managedTypes[obj.managedTypesArrayIndex];
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
                        id = m_uniqueId++,
                        depth = root.depth + 1,
                        displayName = ""
                    };
                    group.Initialize(m_snapshot, type);

                    lookup[hash] = parent = group;
                    root.AddChild(group);
                }

                var item = new ManagedObjectItem
                {
                    id = m_uniqueId++,
                    depth = parent.depth + 1,
                    displayName = ""
                };
                item.Initialize(this, m_snapshot, obj);
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
                    m_managedObjectCount += item.count;
                    m_managedObjectSize += item.size;
                }
            }

            progress.value = 1;
        }
    }
}
