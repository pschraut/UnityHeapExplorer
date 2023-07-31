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
    /// An <see cref="PackedGCHandle"/> index validated against a <see cref="PackedMemorySnapshot"/>.
    /// </summary>
    public readonly struct RichGCHandle
    {
        public readonly PackedMemorySnapshot snapshot;
        public readonly int gcHandlesArrayIndex;

        public RichGCHandle(PackedMemorySnapshot snapshot, int gcHandlesArrayIndex) {
            if (gcHandlesArrayIndex >= snapshot.gcHandles.Length) {
                throw new ArgumentOutOfRangeException(
                    $"{gcHandlesArrayIndex} is out of bounds [0..{snapshot.gcHandles.Length})"
                );
            }
            
            this.snapshot = snapshot;
            this.gcHandlesArrayIndex = gcHandlesArrayIndex;
        }

        public PackedGCHandle packed => snapshot.gcHandles[gcHandlesArrayIndex];

        public Option<RichManagedObject> managedObject =>
            packed.managedObjectsArrayIndex.valueOut(out var index)
                ? Some(new RichManagedObject(snapshot, index))
                : None._;

        public Option<RichNativeObject> nativeObject => managedObject.flatMap(_ => _.nativeObject);

        public ulong managedObjectAddress => packed.target;

        public int size => snapshot.virtualMachineInformation.pointerSize.sizeInBytes();
    }
}
