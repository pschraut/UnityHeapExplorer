using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;

namespace HeapExplorer
{
    public class ArrayPropertyGridItem : PropertyGridItem
    {
        public PackedManagedType arrayType;
        const int kMaxItemsPerChunk = 1024*32;

        public ArrayPropertyGridItem(PropertyGridControl owner, PackedMemorySnapshot snapshot, System.UInt64 address, AbstractMemoryReader memoryReader)
            : base(owner, snapshot, address, memoryReader)
        {
        }

        protected override void OnInitialize()
        {
            var pointer = m_memoryReader.ReadPointer(address);
            var elementType = m_snapshot.managedTypes[arrayType.baseOrElementTypeIndex];
            var dim0Length = address > 0 ? m_memoryReader.ReadArrayLength(address, arrayType, 0) : 0;

            m_type = arrayType;
            typeIndex = arrayType.managedTypesArrayIndex;
            displayType = arrayType.name;
            displayValue = "null";
            allowExpand = dim0Length > 0;
            enabled = pointer > 0;
            icon = HeEditorStyles.GetTypeImage(m_snapshot, arrayType);

            if (pointer != 0)
            {
                displayValue = elementType.name;

                var isJagged = arrayType.name.IndexOf("[][]") != -1;
                if (isJagged)
                {
                    for (var n = 0; n < arrayType.arrayRank; ++n)
                    {
                        var length = m_memoryReader.ReadArrayLength(address, arrayType, n);
                        displayValue += string.Format("[{0}]", length);
                    }
                }
                else
                {
                    displayValue += "[";
                    for (var n = 0; n < arrayType.arrayRank; ++n)
                    {
                        var length = m_memoryReader.ReadArrayLength(address, arrayType, n);

                        displayValue += string.Format("{0}", length);
                        if (n + 1 < arrayType.arrayRank)
                            displayValue += ",";
                    }
                    displayValue += "]";
                }
            }
        }

        protected override void OnBuildChildren(System.Action<BuildChildrenArgs> add)
        {
            if (arrayType.arrayRank == 1)
                BuildOneDimArray(add);

            if (arrayType.arrayRank > 1)
                BuildMultiDimArray(add);
        }

        void BuildOneDimArray(System.Action<BuildChildrenArgs> add)
        {
            var arrayLength = m_memoryReader.ReadArrayLength(address, arrayType);
            var elementType = m_snapshot.managedTypes[arrayType.baseOrElementTypeIndex];

            for (var n = 0; n < Mathf.Min(arrayLength, kMaxItemsPerChunk); ++n)
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
            var arrayLength = m_memoryReader.ReadArrayLength(address, arrayType);
            var elementType = m_snapshot.managedTypes[arrayType.baseOrElementTypeIndex];

            for (var n = 0; n < Mathf.Min(arrayLength, kMaxItemsPerChunk); ++n)
            {
                AddArrayElement(elementType, n, add);
            }

            // an understandable way to name elements of an two dimensional array
            if (arrayType.arrayRank == 2)
            {
                var arrayLength2 = m_memoryReader.ReadArrayLength(address, arrayType, 1);

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
            if (arrayType.arrayRank == 3)
            {
                var arrayLength2 = m_memoryReader.ReadArrayLength(address, arrayType, 1);
                var arrayLength3 = m_memoryReader.ReadArrayLength(address, arrayType, 2);

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
                var pointer = m_memoryReader.ReadPointer(address + (ulong)(elementIndex * m_snapshot.virtualMachineInformation.pointerSize) + (ulong)m_snapshot.virtualMachineInformation.arrayHeaderSize);
                var item = new ArrayPropertyGridItem(m_owner, m_snapshot, pointer, m_memoryReader)
                {
                    depth = this.depth + 1,
                    arrayType = elementType
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
                    args.address = address + (ulong)(elementIndex * elementType.size) + (ulong)m_snapshot.virtualMachineInformation.arrayHeaderSize - (ulong)m_snapshot.virtualMachineInformation.objectHeaderSize;
                    args.memoryReader = new MemoryReader(m_snapshot);
                    add(args);
                }
                else
                {
                    // this is the container node for the array elements.
                    // if we don't add the container, all fields are simply added to the array node itself.
                    // however, we want each array element being groupped
                    var pointer = address + (ulong)(elementIndex * elementType.size) + (ulong)m_snapshot.virtualMachineInformation.arrayHeaderSize;

                    var item = new ArrayElementPropertyGridItem(m_owner, m_snapshot, pointer, new MemoryReader(m_snapshot))
                    {
                        depth = this.depth + 1,
                        type = elementType
                    };
                    item.Initialize();
                    this.AddChild(item);

                    pointer = address + (ulong)(elementIndex * elementType.size) + (ulong)m_snapshot.virtualMachineInformation.arrayHeaderSize - (ulong)m_snapshot.virtualMachineInformation.objectHeaderSize;

                    var args = new BuildChildrenArgs();
                    args.parent = item;
                    args.type = elementType;
                    args.address = pointer;
                    args.memoryReader = new MemoryReader(m_snapshot);
                    add(args);
                }
            }
            else
            {
                // address of element
                var addressOfElement = address + (ulong)(elementIndex * m_snapshot.virtualMachineInformation.pointerSize) + (ulong)m_snapshot.virtualMachineInformation.arrayHeaderSize;
                var pointer = m_memoryReader.ReadPointer(addressOfElement);
                if (pointer != 0)
                {
                    var i = m_snapshot.FindManagedObjectTypeOfAddress(pointer);
                    if (i != -1)
                        elementType = m_snapshot.managedTypes[i];
                }

                // this is the container node for the reference type.
                // if we don't add the container, all fields are simply added to the array node itself.
                // however, we want each array element being groupped
                var item = new ArrayElementPropertyGridItem(m_owner, m_snapshot, addressOfElement, m_memoryReader)
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
                    args.memoryReader = new MemoryReader(m_snapshot);
                    add(args);
                }
            }
        }
    }
}
