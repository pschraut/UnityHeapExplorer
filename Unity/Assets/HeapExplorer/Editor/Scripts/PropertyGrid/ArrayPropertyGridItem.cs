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
    public class ArrayPropertyGridItem : PropertyGridItem
    {
        const int k_MaxItemsPerChunk = 1024*32;

        public ArrayPropertyGridItem(PropertyGridControl owner, PackedMemorySnapshot snapshot, System.UInt64 address, AbstractMemoryReader memoryReader)
            : base(owner, snapshot, address, memoryReader)
        {
        }

        protected override void OnInitialize()
        {
            var pointer = m_MemoryReader.ReadPointer(address);
            var elementType = m_Snapshot.managedTypes[type.baseOrElementTypeIndex];
            var dim0Length = address > 0 ? m_MemoryReader.ReadArrayLength(address, type, 0) : 0;
            
            displayType = type.name;
            displayValue = "null";
            isExpandable = dim0Length > 0;
            enabled = pointer > 0;
            icon = HeEditorStyles.GetTypeImage(m_Snapshot, type);

            if (pointer != 0)
            {
                displayValue = elementType.name;

                var isJagged = type.name.IndexOf("[][]") != -1;
                if (isJagged)
                {
                    for (var n = 0; n < type.arrayRank; ++n)
                    {
                        var length = m_MemoryReader.ReadArrayLength(address, type, n);
                        displayValue += string.Format("[{0}]", length);
                    }
                }
                else
                {
                    displayValue += "[";
                    for (var n = 0; n < type.arrayRank; ++n)
                    {
                        var length = m_MemoryReader.ReadArrayLength(address, type, n);

                        displayValue += string.Format("{0}", length);
                        if (n + 1 < type.arrayRank)
                            displayValue += ",";
                    }
                    displayValue += "]";
                }
            }
        }

        protected override void OnBuildChildren(System.Action<BuildChildrenArgs> add)
        {
            if (type.arrayRank == 1)
                BuildOneDimArray(add);

            if (type.arrayRank > 1)
                BuildMultiDimArray(add);
        }

        void BuildOneDimArray(System.Action<BuildChildrenArgs> add)
        {
            var arrayLength = m_MemoryReader.ReadArrayLength(address, type);
            var elementType = m_Snapshot.managedTypes[type.baseOrElementTypeIndex];

            for (var n = 0; n < Mathf.Min(arrayLength, k_MaxItemsPerChunk); ++n)
            {
                AddArrayElement(elementType, n, add);
            }

            for (var n = 0; n < children.Count; ++n)
            {
                var child = children[n] as PropertyGridItem;
                if (child != null)
                    child.displayName = string.Format("[{0}]", n);
            }
        }

        void BuildMultiDimArray(System.Action<BuildChildrenArgs> add)
        {
            var arrayLength = m_MemoryReader.ReadArrayLength(address, type);
            var elementType = m_Snapshot.managedTypes[type.baseOrElementTypeIndex];

            for (var n = 0; n < Mathf.Min(arrayLength, k_MaxItemsPerChunk); ++n)
            {
                AddArrayElement(elementType, n, add);
            }

            // an understandable way to name elements of an two dimensional array
            if (type.arrayRank == 2)
            {
                var arrayLength2 = m_MemoryReader.ReadArrayLength(address, type, 1);

                var x = 0;
                var y = 0;

                for (var n = 0; n < children.Count; ++n)
                {
                    var child = children[n] as PropertyGridItem;
                    if (child != null)
                        child.displayName = string.Format("[{0},{1}]", y, x);

                    x++;
                    if (x >= arrayLength2)
                    {
                        x = 0;
                        y++;
                    }
                }
            }

            // complicated way of naming elements of three and more dimensional arrays
            if (type.arrayRank == 3)
            {
                var arrayLength2 = m_MemoryReader.ReadArrayLength(address, type, 1);
                var arrayLength3 = m_MemoryReader.ReadArrayLength(address, type, 2);

                var x = 0;
                var y = 0;
                var z = 0;

                for (var n = 0; n < children.Count; ++n)
                {
                    var child = children[n] as PropertyGridItem;
                    if (child != null)
                        child.displayName = string.Format("[{0},{1},{2}]", z, y, x);

                    x++;
                    if (x >= arrayLength2)
                    {
                        x = 0;
                        y++;
                        if (y >= arrayLength3)
                        {
                            y = 0;
                            z++;
                        }
                    }
                }
            }
        }

        void AddArrayElement(PackedManagedType elementType, int elementIndex, System.Action<BuildChildrenArgs> add)
        {
            if (elementType.isArray)
            {
                var pointer = m_MemoryReader.ReadPointer(address + (ulong)(elementIndex * m_Snapshot.virtualMachineInformation.pointerSize) + (ulong)m_Snapshot.virtualMachineInformation.arrayHeaderSize);
                var item = new ArrayPropertyGridItem(m_Owner, m_Snapshot, pointer, m_MemoryReader)
                {
                    depth = this.depth + 1,
                    type = elementType
                };
                item.OnInitialize();
                this.AddChild(item);
            }
            else if (elementType.isValueType)
            {
                if (elementType.isPrimitive)
                {
                    var args = new BuildChildrenArgs();
                    args.parent = this;
                    args.type = elementType;
                    args.address = address + (ulong)(elementIndex * elementType.size) + (ulong)m_Snapshot.virtualMachineInformation.arrayHeaderSize - (ulong)m_Snapshot.virtualMachineInformation.objectHeaderSize;
                    args.memoryReader = new MemoryReader(m_Snapshot);
                    add(args);
                }
                else
                {
                    // this is the container node for the array elements.
                    // if we don't add the container, all fields are simply added to the array node itself.
                    // however, we want each array element being groupped
                    var pointer = address + (ulong)(elementIndex * elementType.size) + (ulong)m_Snapshot.virtualMachineInformation.arrayHeaderSize;

                    var item = new ArrayElementPropertyGridItem(m_Owner, m_Snapshot, pointer, new MemoryReader(m_Snapshot))
                    {
                        depth = this.depth + 1,
                        type = elementType
                    };
                    item.Initialize();
                    this.AddChild(item);

                    pointer = address + (ulong)(elementIndex * elementType.size) + (ulong)m_Snapshot.virtualMachineInformation.arrayHeaderSize - (ulong)m_Snapshot.virtualMachineInformation.objectHeaderSize;

                    var args = new BuildChildrenArgs();
                    args.parent = item;
                    args.type = elementType;
                    args.address = pointer;
                    args.memoryReader = new MemoryReader(m_Snapshot);
                    add(args);
                }
            }
            else
            {
                // address of element
                var addressOfElement = address + (ulong)(elementIndex * m_Snapshot.virtualMachineInformation.pointerSize) + (ulong)m_Snapshot.virtualMachineInformation.arrayHeaderSize;
                var pointer = m_MemoryReader.ReadPointer(addressOfElement);
                if (pointer != 0)
                {
                    var i = m_Snapshot.FindManagedObjectTypeOfAddress(pointer);
                    if (i != -1)
                        elementType = m_Snapshot.managedTypes[i];
                }

                // this is the container node for the reference type.
                // if we don't add the container, all fields are simply added to the array node itself.
                // however, we want each array element being groupped
                var item = new ArrayElementPropertyGridItem(m_Owner, m_Snapshot, addressOfElement, m_MemoryReader)
                {
                    depth = this.depth + 1,
                    type = elementType
                };
                item.Initialize();
                this.AddChild(item);

                // Only add the actual element fields if the pointer is valid
                if (pointer != 0)
                {
                    var args = new BuildChildrenArgs();
                    args.parent = item;
                    args.type = elementType;
                    args.address = pointer;
                    args.memoryReader = new MemoryReader(m_Snapshot);
                    add(args);
                }
            }
        }
    }
}
