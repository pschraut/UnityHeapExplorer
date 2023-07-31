//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//
using System;
using HeapExplorer.Utilities;

namespace HeapExplorer {
    /// <summary>
    /// Similar to <see cref="PackedManagedField"/> but can only represent static fields and thus has the
    /// <see cref="staticFieldsArrayIndex"/> field.
    /// </summary>
    [Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    public readonly struct PackedManagedStaticField {
        /// <summary>
        /// The index into <see cref="PackedMemorySnapshot.managedTypes"/> of the type this field belongs to.
        /// </summary>
        public readonly PInt managedTypesArrayIndex;

        /// <summary>
        /// The index into the <see cref="PackedManagedType.fields"/> array
        /// </summary>
        public readonly PInt fieldIndex;

        /// <summary>
        /// The index into the <see cref="PackedMemorySnapshot.managedStaticFields"/> array
        /// </summary>
        public readonly PInt staticFieldsArrayIndex;

        public PackedManagedStaticField(PInt managedTypesArrayIndex, PInt fieldIndex, PInt staticFieldsArrayIndex) {
            this.managedTypesArrayIndex = managedTypesArrayIndex;
            this.fieldIndex = fieldIndex;
            this.staticFieldsArrayIndex = staticFieldsArrayIndex;
        }
    }
}
