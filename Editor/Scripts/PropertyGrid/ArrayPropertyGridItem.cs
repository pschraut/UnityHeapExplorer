//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//
using HeapExplorer.Utilities;
using UnityEngine;

namespace HeapExplorer
{
    public class ArrayPropertyGridItem : PropertyGridItem
    {
        const int k_MaxItemsPerChunk = 1024*32;
        
        PInt arrayRank => type.arrayRank.getOrThrow("this should be only invoked for arrays!");

        public ArrayPropertyGridItem(PropertyGridControl owner, PackedMemorySnapshot snapshot, System.UInt64 address, AbstractMemoryReader memoryReader)
            : base(owner, snapshot, address, memoryReader)
        {
        }

        protected override void OnInitialize()
        {
            var pointer = m_MemoryReader.ReadPointer(address).getOrThrow();
            var elementType = m_Snapshot.managedTypes[type.baseOrElementTypeIndex.getOrThrow()];
            var arrayRank = this.arrayRank;
            var dim0Length = address > 0 ? m_MemoryReader.ReadArrayLength(address, type, arrayRank, 0).getOrThrow() : 0;

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
                    for (var n = 0; n < arrayRank; ++n)
                    {
                        var length = m_MemoryReader.ReadArrayLength(address, type, arrayRank, n);
                        displayValue += $"[{length}]";
                    }
                }
                else
                {
                    displayValue += "[";
                    for (var n = 0; n < arrayRank; ++n)
                    {
                        var length = m_MemoryReader.ReadArrayLength(address, type, arrayRank, n);

                        displayValue += $"{length}";
                        if (n + 1 < arrayRank)
                            displayValue += ",";
                    }
                    displayValue += "]";
                }
            }
        }

        protected override void OnBuildChildren(System.Action<BuildChildrenArgs> add) {
            
            if (arrayRank == 1)
                BuildOneDimArray(add);

            if (arrayRank > 1)
                BuildMultiDimArray(add);
        }

        void BuildOneDimArray(System.Action<BuildChildrenArgs> add)
        {
            var arrayLength = m_MemoryReader.ReadArrayLength(address, arrayRank).getOrThrow();
            var elementType = m_Snapshot.managedTypes[type.baseOrElementTypeIndex.getOrThrow()];

            for (var n = 0; n < Mathf.Min(arrayLength, k_MaxItemsPerChunk); ++n)
            {
                AddArrayElement(elementType, n, add);
            }

            for (var n = 0; n < children.Count; ++n)
            {
                var child = children[n] as PropertyGridItem;
                if (child != null)
                    child.displayName = $"[{n}]";
            }
        }

        void BuildMultiDimArray(System.Action<BuildChildrenArgs> add) {
            var arrayRank = this.arrayRank;
            var arrayLength = m_MemoryReader.ReadArrayLength(address, arrayRank).getOrThrow();
            var elementType = m_Snapshot.managedTypes[type.baseOrElementTypeIndex.getOrThrow()];

            for (var n = 0; n < Mathf.Min(arrayLength, k_MaxItemsPerChunk); ++n)
            {
                AddArrayElement(elementType, n, add);
            }

            // an understandable way to name elements of an two dimensional array
            if (arrayRank == 2)
            {
                var arrayLength2 = m_MemoryReader.ReadArrayLength(address, type, arrayRank, 1).getOrThrow();

                var x = 0;
                var y = 0;

                for (var n = 0; n < children.Count; ++n)
                {
                    var child = children[n] as PropertyGridItem;
                    if (child != null)
                        child.displayName = $"[{y},{x}]";

                    x++;
                    if (x >= arrayLength2)
                    {
                        x = 0;
                        y++;
                    }
                }
            }

            // complicated way of naming elements of three and more dimensional arrays
            if (arrayRank == 3)
            {
                var arrayLength2 = m_MemoryReader.ReadArrayLength(address, type, arrayRank, 1).getOrThrow();
                var arrayLength3 = m_MemoryReader.ReadArrayLength(address, type, arrayRank, 2).getOrThrow();

                var x = 0;
                var y = 0;
                var z = 0;

                for (var n = 0; n < children.Count; ++n)
                {
                    var child = children[n] as PropertyGridItem;
                    if (child != null)
                        child.displayName = $"[{z},{y},{x}]";

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

        void AddArrayElement(
            PackedManagedType elementType, int elementIndex, System.Action<BuildChildrenArgs> add
        ) {
            if (elementType.isArray)
            {
                var pointer = m_MemoryReader.ReadPointer(
                    address 
                    + (ulong)(elementIndex * m_Snapshot.virtualMachineInformation.pointerSize.sizeInBytes()) 
                    + m_Snapshot.virtualMachineInformation.arrayHeaderSize
                ).getOrThrow();
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
                if (elementType.size.valueOut(out var elementTypeSize)) {
                    if (elementType.isPrimitive) {
                        var args = new BuildChildrenArgs {
                            parent = this,
                            type = elementType,
                            address = address
                                      + (ulong) (elementIndex * elementTypeSize)
                                      + m_Snapshot.virtualMachineInformation.arrayHeaderSize
                                      - m_Snapshot.virtualMachineInformation.objectHeaderSize,
                            memoryReader = new MemoryReader(m_Snapshot)
                        };
                        add(args);
                    }
                    else {
                        // this is the container node for the array elements.
                        // if we don't add the container, all fields are simply added to the array node itself.
                        // however, we want each array element being groupped
                        var pointer = address
                                      + (ulong) (elementIndex * elementTypeSize)
                                      + m_Snapshot.virtualMachineInformation.arrayHeaderSize;

                        var item = new ArrayElementPropertyGridItem(m_Owner, m_Snapshot, pointer,
                            new MemoryReader(m_Snapshot)) {
                            depth = this.depth + 1,
                            type = elementType
                        };
                        item.Initialize();
                        this.AddChild(item);

                        pointer =
                            address
                            + (ulong) (elementIndex * elementTypeSize)
                            + m_Snapshot.virtualMachineInformation.arrayHeaderSize
                            - m_Snapshot.virtualMachineInformation.objectHeaderSize;

                        var args = new BuildChildrenArgs();
                        args.parent = item;
                        args.type = elementType;
                        args.address = pointer;
                        args.memoryReader = new MemoryReader(m_Snapshot);
                        add(args);
                    }
                }
                else {
                    Utils.reportInvalidSizeError(elementType, m_Snapshot.reportedErrors);
                }
            }
            else
            {
                // address of element
                var addressOfElement = 
                    address 
                    + (ulong)(elementIndex * m_Snapshot.virtualMachineInformation.pointerSize.sizeInBytes()) 
                    + m_Snapshot.virtualMachineInformation.arrayHeaderSize;
                var pointer = m_MemoryReader.ReadPointer(addressOfElement).getOrThrow();
                if (pointer != 0)
                {
                    if (m_Snapshot.FindManagedObjectTypeOfAddress(pointer).valueOut(out var i))
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
