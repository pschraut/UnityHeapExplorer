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
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    public struct PackedManagedObject
    {
        // The address of the managed object
        public System.UInt64 address;

        // If this object is a static field
        public System.Byte[] staticBytes;

        // The managed type of this managed object
        public System.Int32 managedTypesArrayIndex;

        // An index into the managedObjects array of the snapshot that stores this managed object
        public System.Int32 managedObjectsArrayIndex;

        // The index into the gcHandles array of the snapshot that is connected to this managed object, if any.
        public System.Int32 gcHandlesArrayIndex;

        // The index into the nativeObjects array of the snapshot that is connected to this managed object, if any.
        public System.Int32 nativeObjectsArrayIndex;

        // Size in bytes of this object.
        // ValueType arrays = count * sizeof(element)
        // ReferenceType arrays = count * sizeof(pointer)
        // String = length * sizeof(wchar) + strlen("\0\0")
        public System.Int32 size;

        public static PackedManagedObject New()
        {
            return new PackedManagedObject()
            {
                managedTypesArrayIndex = -1,
                managedObjectsArrayIndex = -1,
                gcHandlesArrayIndex = -1,
                nativeObjectsArrayIndex = -1,
            };
        }
    }
}
