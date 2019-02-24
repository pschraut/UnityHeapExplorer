//
// Heap Explorer for Unity. Copyright (c) 2019 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://bitbucket.org/pschraut/unityheapexplorer/
//
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeapExplorer
{
    [Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    public struct PackedManagedStaticField
    {
        // The index into PackedMemorySnapshot.typeDescriptions of the type this field belongs to.
        public System.Int32 managedTypesArrayIndex;

        // The index into the typeDescription.fields array
        public System.Int32 fieldIndex;

        // The index into the PackedMemorySnapshot.staticFields array
        public System.Int32 staticFieldsArrayIndex;
    }
}
