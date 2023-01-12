//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//

using HeapExplorer.Utilities;

namespace HeapExplorer
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    public partial struct PackedManagedObject
    {
        /// <summary>
        /// The address of the managed object
        /// </summary>
        public readonly ulong address;

        /// <summary>
        /// `Some` if this object is a static field.
        /// </summary>
        public Option<byte[]> staticBytes;

        /// <summary>
        /// An index into the <see cref="PackedMemorySnapshot.managedTypes"/> array that stores this managed type
        /// </summary>
        public PInt managedTypesArrayIndex;

        /// <summary>
        /// An index into the <see cref="PackedMemorySnapshot.managedObjects"/> array that stores this managed object
        /// </summary>
        public ArrayIndex managedObjectsArrayIndex;

        /// <summary>
        /// The index into the <see cref="PackedMemorySnapshot.gcHandles"/> array of the snapshot that is connected to
        /// this managed object, if any.
        /// </summary>
        public Option<PInt> gcHandlesArrayIndex;

        /// <summary>
        /// The index into the <see cref="PackedMemorySnapshot.nativeObjects"/> array of the snapshot that is connected
        /// to this managed object, if any.
        /// </summary>
        public Option<PInt> nativeObjectsArrayIndex;

        /// <summary>
        /// Size in bytes of this object. `None` if the size is unknown.<br/>
        /// ValueType arrays = count * sizeof(element)<br/>
        /// ReferenceType arrays = count * sizeof(pointer)<br/>
        /// String = length * sizeof(wchar) + strlen("\0\0")
        /// </summary>
        public Option<uint> size;

        public PackedManagedObject(
            ulong address, Option<byte[]> staticBytes, PInt managedTypesArrayIndex, ArrayIndex managedObjectsArrayIndex, 
            Option<PInt> gcHandlesArrayIndex, Option<PInt> nativeObjectsArrayIndex, Option<uint> size
        ) {
            this.address = address;
            this.staticBytes = staticBytes;
            this.managedTypesArrayIndex = managedTypesArrayIndex;
            this.managedObjectsArrayIndex = managedObjectsArrayIndex;
            this.gcHandlesArrayIndex = gcHandlesArrayIndex;
            this.nativeObjectsArrayIndex = nativeObjectsArrayIndex;
            this.size = size;
        }

        public static PackedManagedObject New(
            ulong address,
            ArrayIndex managedObjectsArrayIndex,
            PInt managedTypesArrayIndex,
            Option<PInt> gcHandlesArrayIndex = default,
            Option<PInt> nativeObjectsArrayIndex = default,
            Option<uint> size = default,
            Option<byte[]> staticBytes = default
        ) =>
            new PackedManagedObject(
                address: address,
                managedTypesArrayIndex: managedTypesArrayIndex,
                managedObjectsArrayIndex: managedObjectsArrayIndex,
                gcHandlesArrayIndex: gcHandlesArrayIndex,
                nativeObjectsArrayIndex: nativeObjectsArrayIndex,
                size: size, 
                staticBytes: staticBytes
            );
    }
}
