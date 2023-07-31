﻿//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

namespace HeapExplorer
{
    /// <summary>
    /// A dump of a piece of memory from the player that's being profiled.
    /// </summary>
    [Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    public struct PackedMemorySection
    {
        /// <summary>
        /// The actual bytes of the memory dump.
        /// </summary>
        public byte[] bytes;

        /// <summary>
        /// The start address of this piece of memory. Inclusive.
        /// </summary>
        public System.UInt64 startAddress;

        /// <summary>
        /// The end address of this piece of memory. Exclusive.
        /// </summary>
        public ulong endAddress {
            get {
                return startAddress + size;
            }
        }

        /// <summary>
        /// The index into the <see cref="PackedMemorySnapshot.managedHeapSections"/> array
        /// </summary>
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

        /// <summary>
        /// Does this memory section contain the provided memory address?
        /// </summary>
        public bool containsAddress(ulong address) {
            return address >= startAddress && address < endAddress;
        }
        
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
                        stateString =
                            $"Loading Managed Heap Sections\n{n + 1}/{length}, {((n + 1) / (float) length) * 100:F0}% done";

                    var count = reader.ReadInt32();
                    value[n].bytes = reader.ReadBytes(count);
                    value[n].startAddress = reader.ReadUInt64();
                    value[n].arrayIndex = -1;
                }
            }
        }

        public static PackedMemorySection[] FromMemoryProfiler(UnityEditor.Profiling.Memory.Experimental.PackedMemorySnapshot snapshot)
        {
            var source = snapshot.managedHeapSections;
            var value = new PackedMemorySection[source.GetNumEntries()];

            var sourceBytes = new byte[source.bytes.GetNumEntries()][];
            source.bytes.GetEntries(0, source.bytes.GetNumEntries(), ref sourceBytes);

            var sourceStartAddresses = new ulong[source.startAddress.GetNumEntries()];
            source.startAddress.GetEntries(0, source.startAddress.GetNumEntries(), ref sourceStartAddresses);

            for (int n = 0, nend = value.Length; n < nend; ++n)
            {
                value[n] = new PackedMemorySection
                {
                    bytes = sourceBytes[n],
                    startAddress = sourceStartAddresses[n],
                    arrayIndex = -1
                };
            }
            return value;
        }
    }

}
