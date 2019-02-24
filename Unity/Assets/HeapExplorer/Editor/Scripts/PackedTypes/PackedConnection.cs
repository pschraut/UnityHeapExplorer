//
// Heap Explorer for Unity. Copyright (c) 2019 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://bitbucket.org/pschraut/unityheapexplorer/
//
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeapExplorer
{
    // A pair of from and to indices describing what object keeps what other object alive.
    [Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    public struct PackedConnection
    {
        public enum Kind : byte //System.Byte
        {
            None = 0,
            GCHandle = 1,
            Native = 2,
            Managed = 3, // managed connections are NOT in the snapshot, we add them ourselfs.
            StaticField = 4, // static connections are NOT in the snapshot, we add them ourself.

            // Must not get greater than 11, otherwise ComputeConnectionKey() fails!
        }

        public System.Int32 from; // Index into a gcHandles, nativeObjects.
        public System.Int32 to; // Index into a gcHandles, nativeObjects.

        [NonSerialized] public Kind fromKind;
        [NonSerialized] public Kind toKind;

        const System.Int32 k_Version = 1;

        public static void Write(System.IO.BinaryWriter writer, PackedConnection[] value)
        {
            writer.Write(k_Version);
            writer.Write(value.Length);

            for (int n = 0, nend = value.Length; n < nend; ++n)
            {
                writer.Write(value[n].from);
                writer.Write(value[n].to);
            }
        }

        public static void Read(System.IO.BinaryReader reader, out PackedConnection[] value, out string stateString)
        {
            value = new PackedConnection[0];
            stateString = "";

            var version = reader.ReadInt32();
            if (version >= 1)
            {
                var length = reader.ReadInt32();
                //stateString = string.Format("Loading {0} Object Connections", length);
                value = new PackedConnection[length];
                if (length == 0)
                    return;

                var onePercent = Math.Max(1, value.Length / 100);
                for (int n = 0, nend = value.Length; n < nend; ++n)
                {
                    if ((n % onePercent) == 0)
                        stateString = string.Format("Loading Object Connections\n{0}/{1}, {2:F0}% done", n + 1, length, ((n + 1) / (float)length) * 100);

                    value[n].from = reader.ReadInt32();
                    value[n].to = reader.ReadInt32();
                }
            }
        }

        public static PackedConnection[] FromMemoryProfiler(UnityEditor.MemoryProfiler.Connection[] source)
        {
            PackedConnection[] value = null;
            try
            {
                value = new PackedConnection[source.Length];
            }
            catch
            {
                Debug.LogErrorFormat("HeapExplorer: Failed to allocate 'new PackedConnection[{0}]'.", source.Length);
                throw;
            }

            for (int n = 0, nend = source.Length; n < nend; ++n)
            {
                value[n] = new PackedConnection
                {
                    from = source[n].from,
                    to = source[n].to,
                };
            }
            return value;
        }
    }
}
