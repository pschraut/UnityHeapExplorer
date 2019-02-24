//
// Heap Explorer for Unity. Copyright (c) 2019 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://bitbucket.org/pschraut/unityheapexplorer/
//
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

namespace HeapExplorer
{
    // A dump of a piece of memory from the player that's being profiled.
    [Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    public struct PackedMemorySection
    {
        // The actual bytes of the memory dump.
        public System.Byte[] bytes;

        // The start address of this piece of memory.
        public System.UInt64 startAddress;

        // The index into the snapshot.managedHeapSections array
        [System.NonSerialized]
        public int arrayIndex;

        public ulong size
        {
            get
            {
                if (bytes != null)
                    return (ulong)bytes.LongLength;
                return 0;
            }
        }

        const System.Int32 k_Version = 1;

        public static void Write(System.IO.BinaryWriter writer, PackedMemorySection[] value)
        {
            writer.Write(k_Version);
            writer.Write(value.Length);

            for (int n = 0, nend = value.Length; n < nend; ++n)
            {
                writer.Write((System.Int32)value[n].bytes.Length);
                writer.Write(value[n].bytes);
                writer.Write(value[n].startAddress);
            }
        }

        public static void Read(System.IO.BinaryReader reader, out PackedMemorySection[] value, out string stateString)
        {
            value = new PackedMemorySection[0];
            stateString = "";

            var version = reader.ReadInt32();
            if (version >= 1)
            {
                var length = reader.ReadInt32();
                value = new PackedMemorySection[length];

                var onePercent = Math.Max(1, value.Length / 100);
                for (int n = 0, nend = value.Length; n < nend; ++n)
                {
                    if ((n % onePercent) == 0)
                        stateString = string.Format("Loading Managed Heap Sections\n{0}/{1}, {2:F0}% done", n + 1, length, ((n + 1) / (float)length) * 100);

                    var count = reader.ReadInt32();
                    value[n].bytes = reader.ReadBytes(count);
                    value[n].startAddress = reader.ReadUInt64();
                    value[n].arrayIndex = -1;
                }
            }
        }

        public static PackedMemorySection[] FromMemoryProfiler(UnityEditor.MemoryProfiler.MemorySection[] source)
        {
            var value = new PackedMemorySection[source.Length];

            for (int n = 0, nend = source.Length; n < nend; ++n)
            {
                value[n] = new PackedMemorySection
                {
                    bytes = source[n].bytes,
                    startAddress = source[n].startAddress,
                    arrayIndex = -1
                };
            }
            return value;
        }
    }

}
