//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//

using System;
using HeapExplorer.Utilities;
using static HeapExplorer.Utilities.Option;

namespace HeapExplorer
{
    /// <summary>
    /// An <see cref="PackedManagedObject.ArrayIndex"/> validated against a <see cref="PackedMemorySnapshot"/>.
    /// </summary>
    public readonly struct RichManagedObject
    {
        public RichManagedObject(PackedMemorySnapshot snapshot, PackedManagedObject.ArrayIndex managedObjectsArrayIndex)
        {
            if (managedObjectsArrayIndex.isStatic)
                throw new ArgumentException(
                    $"{managedObjectsArrayIndex} is static while we're trying to create a {nameof(RichManagedObject)}!"
                );
            if (managedObjectsArrayIndex.index >= snapshot.managedObjects.Length) {
                throw new ArgumentOutOfRangeException(
                    $"{managedObjectsArrayIndex} is out of bounds [0..{snapshot.managedObjects.Length})"
                );
            }
            this.snapshot = snapshot;
            this.managedObjectsArrayIndex = managedObjectsArrayIndex.index;
        }

        public PackedManagedObject packed => snapshot.managedObjects[managedObjectsArrayIndex];

        public ulong address => packed.address;

        public uint size => packed.size.getOrElse(0);

        public RichManagedType type => new RichManagedType(snapshot, packed.managedTypesArrayIndex);

        public Option<RichGCHandle> gcHandle =>
            packed.gcHandlesArrayIndex.valueOut(out var index) 
                ? Some(new RichGCHandle(snapshot, index)) 
                : None._;

        public Option<RichNativeObject> nativeObject =>
            packed.nativeObjectsArrayIndex.valueOut(out var index) 
                ? Some(new RichNativeObject(snapshot, index))
                : None._;

        public override string ToString() =>
            // We output the address with '0x' prefix to make it comfortable to copy and paste it into an exact search
            // field.
            $"Addr: 0x{address:X}, Type: {type.name}";

        public readonly PackedMemorySnapshot snapshot;
        
        /// <summary>Index into <see cref="PackedMemorySnapshot.managedObjects"/>.</summary>
        public readonly int managedObjectsArrayIndex;
    }
}
