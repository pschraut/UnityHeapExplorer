//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//

using System;
using UnityEngine;

namespace HeapExplorer
{
    /// <summary>
    /// An <see cref="PackedManagedType"/> index validated against a <see cref="PackedMemorySnapshot"/>.
    /// </summary>
    public readonly struct RichManagedType
    {
        public RichManagedType(PackedMemorySnapshot snapshot, int managedTypesArrayIndex) {
            if (managedTypesArrayIndex < 0 || managedTypesArrayIndex >= snapshot.managedTypes.Length)
                throw new ArgumentOutOfRangeException(
                    $"managedTypesArrayIndex ({managedTypesArrayIndex}) was out of bounds [0..{snapshot.managedTypes.Length})"
                );
            
            this.snapshot = snapshot;
            this.managedTypesArrayIndex = managedTypesArrayIndex;
        }

        /// <summary>
        /// Gets the underlying low-level type.
        /// </summary>
        public PackedManagedType packed => snapshot.managedTypes[managedTypesArrayIndex];

        /// <summary>
        /// Gets the name of the type.
        /// </summary>
        public string name => packed.name;

        /// <summary>
        /// Gets the name of the assembly where this type is stored in.
        /// </summary>
        public string assemblyName => packed.assembly;

        public bool FindField(string name, out PackedManagedField packedManagedField)
        {
            var guard = 0;
            var me = packed;
            while (me.managedTypesArrayIndex != -1)
            {
                for (var n=0; n<me.fields.Length; ++n)
                {
                    if (me.fields[n].name == name)
                    {
                        packedManagedField = me.fields[n];
                        return true;
                    }
                }

                if (++guard > 64) {
                    Debug.LogError($"Guard hit in {nameof(FindField)}({name}) at type '{me.name}'");
                    break;
                }

                if (!me.baseOrElementTypeIndex.valueOut(out var baseOrElementTypeIndex))
                    break;

                me = snapshot.managedTypes[baseOrElementTypeIndex];
            }

            packedManagedField = default;
            return false;
        }

        /// <summary>
        /// Gets whether this native type is a subclass of the specified type 't'.
        /// </summary>
        public bool IsSubclassOf(PackedManagedType t)
        {
            if (t.managedTypesArrayIndex == -1)
                return false;

            var me = packed;
            if (me.managedTypesArrayIndex == t.managedTypesArrayIndex)
                return true;

            var guard = 0;
            while (me.baseOrElementTypeIndex.valueOut(out var baseOrElementTypeIndex))
            {
                if (++guard > 64) {
                    Debug.LogError($"Guard hit in {nameof(IsSubclassOf)}({t.name}) at type '{me.name}'");
                    break; // no inheritance should have more depths than this
                }

                if (baseOrElementTypeIndex == t.managedTypesArrayIndex)
                    return true;

                me = snapshot.managedTypes[baseOrElementTypeIndex];
            }

            return false;
        }

        public readonly PackedMemorySnapshot snapshot;
        public readonly int managedTypesArrayIndex;
    }
}
