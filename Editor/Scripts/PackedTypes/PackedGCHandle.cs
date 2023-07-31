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
    /// A description of a GC handle used by the virtual machine.
    /// </summary>
    /// <remarks>
    /// A GCHandle is a struct that contains a handle to an object.
    /// It's mainly used for holding onto a managed object that gets passed to the unmanaged world to prevent the GC from collecting the object.
    /// You can also create a Pinned GCHandle to a managed object and retrieve the object's address in memory.
    /// https://blogs.msdn.microsoft.com/clyon/2005/03/18/the-truth-about-gchandles/
    /// </remarks>
    [Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    public readonly struct PackedGCHandle
    {
        /// <summary>
        /// The address of the managed object that the GC handle is referencing.
        /// </summary>
        public readonly ulong target;

        /// <summary>
        /// Index into <see cref="PackedMemorySnapshot.gcHandles"/>.
        /// </summary>
        [NonSerialized] public readonly PInt gcHandlesArrayIndex;
        
        /// <inheritdoc cref="PackedManagedObject.ArrayIndex"/>
        [NonSerialized] public readonly Option<PackedManagedObject.ArrayIndex> managedObjectsArrayIndex;

        public PackedGCHandle(
            ulong target, PInt gcHandlesArrayIndex, 
            Option<PackedManagedObject.ArrayIndex> managedObjectsArrayIndex = default
        ) {
            this.target = target;
            this.gcHandlesArrayIndex = gcHandlesArrayIndex;
            this.managedObjectsArrayIndex = managedObjectsArrayIndex;
        }

        public PackedGCHandle withManagedObjectsArrayIndex(PackedManagedObject.ArrayIndex index) =>
            new PackedGCHandle(
                target: target, gcHandlesArrayIndex: gcHandlesArrayIndex, managedObjectsArrayIndex: Some(index)
            );

        const int k_Version = 1;

        public static void Write(System.IO.BinaryWriter writer, PackedGCHandle[] value)
        {
            writer.Write(k_Version);
            writer.Write(value.Length);

            for (int n = 0, nend = value.Length; n < nend; ++n)
            {
                writer.Write(value[n].target);
            }
        }

        public static void Read(System.IO.BinaryReader reader, out PackedGCHandle[] value, out string stateString)
        {
            value = new PackedGCHandle[0];
            stateString = "";

            var version = reader.ReadInt32();
            if (version >= 1)
            {
                var length = reader.ReadInt32();
                stateString = $"Loading {length} GC Handles";
                value = new PackedGCHandle[length];

                for (PInt n = PInt._0, nend = value.LengthP(); n < nend; ++n)
                {
                    var target = reader.ReadUInt64();
                    value[n] = new PackedGCHandle(target: target, gcHandlesArrayIndex: n);
                }
            }
        }

        public static PackedGCHandle[] FromMemoryProfiler(UnityEditor.Profiling.Memory.Experimental.PackedMemorySnapshot snapshot)
        {
            var source = snapshot.gcHandles;
            var value = new PackedGCHandle[source.GetNumEntries()];

            var sourceTargets = new ulong[source.target.GetNumEntries()];
            source.target.GetEntries(0, source.target.GetNumEntries(), ref sourceTargets);

            for (PInt n = PInt._0, nend = value.LengthP(); n < nend; ++n) {
                value[n] = new PackedGCHandle(target: sourceTargets[n], gcHandlesArrayIndex: n);
            }
            return value;
        }
    }
}
