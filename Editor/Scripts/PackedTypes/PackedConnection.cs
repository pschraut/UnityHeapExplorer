//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//
using System;
using System.Collections.Generic;
using HeapExplorer.Utilities;
using UnityEngine;

namespace HeapExplorer
{
    /// <summary>
    /// A pair of from and to indices describing what object keeps what other object alive.
    /// </summary>
    [Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    public readonly struct PackedConnection
    {
        /// <note>
        /// The value must not get greater than 11, otherwise <see cref="Pair.ComputeConnectionKey"/> fails!
        /// </note>
        public enum Kind : byte //System.Byte
        {
            /// <summary><see cref="PackedMemorySnapshot.gcHandles"/></summary>
            GCHandle = 1,
            
            /// <summary><see cref="PackedMemorySnapshot.nativeObjects"/></summary>
            Native = 2,
            
            /// <summary><see cref="PackedMemorySnapshot.managedObjects"/></summary>
            /// <note>Managed connections are NOT in the snapshot, we add them ourselves.</note>
            Managed = 3, 
            
            /// <summary><see cref="PackedMemorySnapshot.managedStaticFields"/></summary>
            /// <note>Static connections are NOT in the snapshot, we add them ourselves.</note>
            StaticField = 4,
        }

        /// <summary>From part of the connection.</summary>
        public readonly struct From {
            public readonly Pair pair;
            
            /// <summary>
            /// The field data for the reference, if the <see cref="Kind"/> is <see cref="Kind.Managed"/> or
            /// <see cref="Kind.StaticField"/> (except for array types, for now). `None` otherwise.
            /// </summary>
            public readonly Option<PackedManagedField> field;

            public From(Pair pair, Option<PackedManagedField> field) {
                this.pair = pair;
                this.field = field;
            }
        }
        
        /// <summary>Named tuple.</summary>
        public readonly struct Pair {
            /// <summary>The connection kind, that is pointing to another object.</summary>
            public readonly Kind kind;
            
            /// <summary>
            /// An index into a snapshot array, depending on specified <see cref="kind"/>. If the kind would be
            /// '<see cref="PackedConnection.Kind.Native"/>', then it must be an index into the
            /// <see cref="PackedMemorySnapshot.nativeObjects"/> array.</summary>
            public readonly int index;

            public Pair(Kind kind, int index) {
                if (index < 0) throw new ArgumentOutOfRangeException(nameof(index), index, "negative index");
                
                this.kind = kind;
                this.index = index;
            }

            public override string ToString() => $"{nameof(Pair)}[{kind} @ {index}]";
            
            public ulong ComputeConnectionKey() {
                var value = ((ulong)kind << 50) + (ulong)index;
                return value;
            }
        }

        /// <inheritdoc cref="From"/>
        public readonly From from;
        
        /// <inheritdoc cref="Pair"/>
        public readonly Pair to;

        public PackedConnection(From from, Pair to) {
            this.from = from;
            this.to = to;
        }

        const int k_Version = 3;

        public static void Write(System.IO.BinaryWriter writer, PackedConnection[] value)
        {
            writer.Write(k_Version);
            writer.Write(value.Length);

            for (int n = 0, nend = value.Length; n < nend; ++n)
            {
                writer.Write((byte)value[n].from.pair.kind);
                writer.Write((byte)value[n].to.kind);
                writer.Write(value[n].from.pair.index);
                writer.Write(value[n].to.index);
                writer.Write(value[n].from.field.isSome);
                {if (value[n].from.field.valueOut(out var field)) {
                    PackedManagedField.Write(writer, field);        
                }}
            }
        }

        public static void Read(System.IO.BinaryReader reader, out PackedConnection[] value, out string stateString)
        {
            value = new PackedConnection[0];
            stateString = "";

            var version = reader.ReadInt32();
            if (version >= 2)
            {
                var length = reader.ReadInt32();
                value = new PackedConnection[length];
                if (length == 0)
                    return;

                var onePercent = Math.Max(1, value.Length / 100);
                for (int n = 0, nend = value.Length; n < nend; ++n)
                {
                    if ((n % onePercent) == 0)
                        stateString =
                            $"Loading Object Connections\n{n + 1}/{length}, {((n + 1) / (float) length) * 100:F0}% done";

                    var fromKind = (Kind)reader.ReadByte();
                    var toKind = (Kind)reader.ReadByte();
                    var fromIndex = reader.ReadInt32();
                    var toIndex = reader.ReadInt32();

                    Option<PackedManagedField> field;
                    if (version >= 3) {
                        var hasField = reader.ReadBoolean();
                        field = hasField
                            ? Option.Some(PackedManagedField.Read(reader).getOrThrow("this should never fail")) 
                            : None._;
                    }
                    else {
                        field = None._;
                    }
                    
                    value[n] = new PackedConnection(
                        from: new From(new Pair(fromKind, fromIndex), field),
                        to: new Pair(toKind, toIndex)
                    );
                }
            }
            else if (version >= 1) {
                throw new Exception("Old file versions are not supported as they do not contain enough data.");
            }
        }

        public static PackedConnection[] FromMemoryProfiler(UnityEditor.Profiling.Memory.Experimental.PackedMemorySnapshot snapshot)
        {
            var result = new List<PackedConnection>(1024*1024);
            var invalidIndices = 0;

            // Get all NativeObject instanceIds
            var nativeObjects = snapshot.nativeObjects;
            var nativeObjectsCount = nativeObjects.GetNumEntries();
            var nativeObjectsInstanceIds = new int[nativeObjects.instanceId.GetNumEntries()];
            nativeObjects.instanceId.GetEntries(0, nativeObjects.instanceId.GetNumEntries(), ref nativeObjectsInstanceIds);

            // Create lookup table from instanceId to NativeObject array index
            var instanceIdToNativeObjectIndex = new Dictionary<int, int>(nativeObjectsInstanceIds.Length);
            for (var n=0; n< nativeObjectsInstanceIds.Length; ++n)
                instanceIdToNativeObjectIndex.Add(nativeObjectsInstanceIds[n], n);

            // Connect NativeObject with its GCHandle
            var nativeObjectsGCHandleIndices = new int[nativeObjects.gcHandleIndex.GetNumEntries()];
            nativeObjects.gcHandleIndex.GetEntries(0, nativeObjects.gcHandleIndex.GetNumEntries(), ref nativeObjectsGCHandleIndices);

            var gcHandles = snapshot.gcHandles;
            var gcHandlesCount = gcHandles.GetNumEntries();

            for (var n=0; n< nativeObjectsGCHandleIndices.Length; ++n)
            {
                // Unity 2019.4.7f1 and earlier (maybe also newer) versions seem to have this issue:
                // (Case 1269293) PackedMemorySnapshot: nativeObjects.gcHandleIndex contains -1 always
                if (nativeObjectsGCHandleIndices[n] < 0 || nativeObjectsGCHandleIndices[n] >= gcHandlesCount)
                {
                    if (nativeObjectsGCHandleIndices[n] != -1)
                        invalidIndices++; // I guess -1 means no connection to a gcHandle

                    continue;
                }

                var packed = new PackedConnection(
                    from: new From(new Pair(Kind.Native, n /* nativeObject index */), field: None._),
                    to: new Pair(Kind.GCHandle, nativeObjectsGCHandleIndices[n] /* gcHandle index */)
                );
                result.Add(packed);
            }

            if (invalidIndices > 0)
                Debug.LogErrorFormat("Reconstructing native to gchandle connections. Found {0} invalid indices into nativeObjectsGCHandleIndices[{1}] array.", invalidIndices, gcHandlesCount);


            // Connect NativeObject with NativeObject

            var source = snapshot.connections;
            int[] sourceFrom = new int[source.from.GetNumEntries()];
            source.from.GetEntries(0, source.from.GetNumEntries(), ref sourceFrom);

            int[] sourceTo = new int[source.to.GetNumEntries()];
            source.to.GetEntries(0, source.to.GetNumEntries(), ref sourceTo);

            var invalidInstanceIDs = 0;
            invalidIndices = 0;

            for (int n = 0, nend = sourceFrom.Length; n < nend; ++n)
            {
                if (!instanceIdToNativeObjectIndex.TryGetValue(sourceFrom[n], out var fromIndex))
                {
                    invalidInstanceIDs++;
                    continue; // NativeObject InstanceID not found
                }

                if (!instanceIdToNativeObjectIndex.TryGetValue(sourceTo[n], out var toIndex))
                {
                    invalidInstanceIDs++;
                    continue; // NativeObject InstanceID not found
                }

                if (fromIndex < 0 || fromIndex >= nativeObjectsCount)
                {
                    invalidIndices++;
                    continue; // invalid index into array
                }

                if (toIndex < 0 || toIndex >= nativeObjectsCount)
                {
                    invalidIndices++;
                    continue; // invalid index into array
                }

                result.Add(new PackedConnection(
                    from: new From(new Pair(Kind.Native, fromIndex), field: None._),
                    to: new Pair(Kind.Native, toIndex)
                ));
            }

            if (invalidIndices > 0)
                Debug.LogErrorFormat("Reconstructing native to native object connections. Found {0} invalid indices into nativeObjectsCount[{1}] array.", invalidIndices, nativeObjectsCount);

            if (invalidInstanceIDs > 0)
                Debug.LogErrorFormat("Reconstructing native to native object connections. Found {0} invalid instanceIDs.", invalidInstanceIDs, nativeObjectsCount);


            return result.ToArray();
        }
    }
}
