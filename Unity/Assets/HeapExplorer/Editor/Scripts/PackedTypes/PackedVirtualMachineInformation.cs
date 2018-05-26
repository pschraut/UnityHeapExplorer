using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

namespace HeapExplorer
{
    // Information about a virtual machine that provided a memory snapshot.
    [Serializable]
    public struct PackedVirtualMachineInformation
    {
        // Size in bytes of a pointer.
        public System.Int32 pointerSize;

        // Size in bytes of the header of each managed object.
        public System.Int32 objectHeaderSize;

        // Size in bytes of the header of an array object.
        public System.Int32 arrayHeaderSize;

        // Offset in bytes inside the object header of an array object where the bounds of the array is stored.
        public System.Int32 arrayBoundsOffsetInHeader;

        // Offset in bytes inside the object header of an array object where the size of the array is stored.
        public System.Int32 arraySizeOffsetInHeader;

        // Allocation granularity in bytes used by the virtual machine allocator.
        public System.Int32 allocationGranularity;

        // A version number that will change when the object layout inside the managed heap will change.
        public System.Int32 heapFormatVersion;

        const System.Int32 k_Version = 1;

        public static void Write(System.IO.BinaryWriter writer, PackedVirtualMachineInformation value)
        {
#if HEAPEXPLORER_WRITE_HEADER
            writer.Write(k_Version);
#endif

            writer.Write(value.pointerSize);
            writer.Write(value.objectHeaderSize);
            writer.Write(value.arrayHeaderSize);
            writer.Write(value.arrayBoundsOffsetInHeader);
            writer.Write(value.arraySizeOffsetInHeader);
            writer.Write(value.allocationGranularity);
            writer.Write(value.heapFormatVersion);
        }

        public static void Read(System.IO.BinaryReader reader, out PackedVirtualMachineInformation value)
        {
            value = new PackedVirtualMachineInformation();

#if HEAPEXPLORER_READ_HEADER
            var version = reader.ReadInt32();
            if (version >= 1)
#endif
            {
                value.pointerSize = reader.ReadInt32();
                value.objectHeaderSize = reader.ReadInt32();
                value.arrayHeaderSize = reader.ReadInt32();
                value.arrayBoundsOffsetInHeader = reader.ReadInt32();
                value.arraySizeOffsetInHeader = reader.ReadInt32();
                value.allocationGranularity = reader.ReadInt32();
                value.heapFormatVersion = reader.ReadInt32();
            }
        }

        public static PackedVirtualMachineInformation FromMemoryProfiler(UnityEditor.MemoryProfiler.VirtualMachineInformation source)
        {
            var value = new PackedVirtualMachineInformation
            {
                pointerSize = source.pointerSize,
                objectHeaderSize = source.objectHeaderSize,
                arrayHeaderSize = source.arrayHeaderSize,
                arrayBoundsOffsetInHeader = source.arrayBoundsOffsetInHeader,
                arraySizeOffsetInHeader = source.arraySizeOffsetInHeader,
                allocationGranularity = source.allocationGranularity,
                heapFormatVersion = source.heapFormatVersion,
            };
            return value;
        }
    }
}
