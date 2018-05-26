using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HeapExplorer
{
    public struct RichGCHandle
    {
        PackedMemorySnapshot m_snapshot;
        int m_gcHandleArrayIndex;
        bool m_isValid;

        public RichGCHandle(PackedMemorySnapshot snapshot, int gcHandlesArrayIndex)
            : this()
        {
            m_snapshot = snapshot;
            m_gcHandleArrayIndex = gcHandlesArrayIndex;
            m_isValid = m_snapshot != null && m_gcHandleArrayIndex >= 0 && m_gcHandleArrayIndex < m_snapshot.gcHandles.Length;
        }

        public PackedGCHandle packed
        {
            get
            {
                if (!m_isValid)
                    return new PackedGCHandle() { gcHandlesArrayIndex = -1, managedObjectsArrayIndex = -1 };

                return m_snapshot.gcHandles[m_gcHandleArrayIndex];
            }
        }

        public PackedMemorySnapshot snapshot
        {
            get
            {
                return m_snapshot;
            }
        }

        public bool isValid
        {
            get
            {
                return m_isValid;
            }
        }

        public RichManagedObject managedObject
        {
            get
            {
                if (m_isValid)
                {
                    var gcHandle = m_snapshot.gcHandles[m_gcHandleArrayIndex];
                    if (gcHandle.managedObjectsArrayIndex >= 0)
                        return new RichManagedObject(m_snapshot, gcHandle.managedObjectsArrayIndex);
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
                if (!m_isValid)
                    return 0;

                return m_snapshot.gcHandles[m_gcHandleArrayIndex].target;
            }
        }

        public int size
        {
            get
            {
                return m_snapshot.virtualMachineInformation.pointerSize;
            }
        }

        public static readonly RichGCHandle invalid = new RichGCHandle()
        {
            m_snapshot = null,
            m_gcHandleArrayIndex = -1
        };
    }
}
