//
// Heap Explorer for Unity. Copyright (c) 2019 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://bitbucket.org/pschraut/unityheapexplorer/
//
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HeapExplorer
{
    public struct RichGCHandle
    {
        PackedMemorySnapshot m_Snapshot;
        int m_GCHandleArrayIndex;

        public RichGCHandle(PackedMemorySnapshot snapshot, int gcHandlesArrayIndex)
            : this()
        {
            m_Snapshot = snapshot;
            m_GCHandleArrayIndex = gcHandlesArrayIndex;
        }

        public PackedGCHandle packed
        {
            get
            {
                if (!isValid)
                    return new PackedGCHandle() { gcHandlesArrayIndex = -1, managedObjectsArrayIndex = -1 };

                return m_Snapshot.gcHandles[m_GCHandleArrayIndex];
            }
        }

        public PackedMemorySnapshot snapshot
        {
            get
            {
                return m_Snapshot;
            }
        }

        public bool isValid
        {
            get
            {
                var value = m_Snapshot != null && m_GCHandleArrayIndex >= 0 && m_GCHandleArrayIndex < m_Snapshot.gcHandles.Length;
                return value;
            }
        }

        public RichManagedObject managedObject
        {
            get
            {
                if (isValid)
                {
                    var gcHandle = m_Snapshot.gcHandles[m_GCHandleArrayIndex];
                    if (gcHandle.managedObjectsArrayIndex >= 0)
                        return new RichManagedObject(m_Snapshot, gcHandle.managedObjectsArrayIndex);
                }

                return RichManagedObject.invalid;
            }
        }

        public RichNativeObject nativeObject
        {
            get
            {
                return managedObject.nativeObject;
            }
        }

        public System.UInt64 managedObjectAddress
        {
            get
            {
                if (!isValid)
                    return 0;

                return m_Snapshot.gcHandles[m_GCHandleArrayIndex].target;
            }
        }

        public int size
        {
            get
            {
                return m_Snapshot.virtualMachineInformation.pointerSize;
            }
        }

        public static readonly RichGCHandle invalid = new RichGCHandle()
        {
            m_Snapshot = null,
            m_GCHandleArrayIndex = -1
        };
    }
}
