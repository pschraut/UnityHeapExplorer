//
// Heap Explorer for Unity. Copyright (c) 2019 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://bitbucket.org/pschraut/unityheapexplorer/
//
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HeapExplorer
{
    public struct RichManagedObject
    {
        public RichManagedObject(PackedMemorySnapshot snapshot, int managedObjectsArrayIndex)
            : this()
        {
            m_Snapshot = snapshot;
            m_ManagedObjectArrayIndex = managedObjectsArrayIndex;
        }

        public PackedManagedObject packed
        {
            get
            {
                if (!isValid)
                    return PackedManagedObject.New();

                return m_Snapshot.managedObjects[m_ManagedObjectArrayIndex];
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
                var value = m_Snapshot != null && m_ManagedObjectArrayIndex >= 0 && m_ManagedObjectArrayIndex < m_Snapshot.managedObjects.Length;
                return value;
            }
        }

        public System.Int32 arrayIndex
        {
            get
            {
                return m_ManagedObjectArrayIndex;
            }
        }

        public System.UInt64 address
        {
            get
            {
                if (!isValid)
                    return 0;

                var mo = m_Snapshot.managedObjects[m_ManagedObjectArrayIndex];
                return mo.address;
            }
        }

        public System.Int32 size
        {
            get
            {
                if (isValid)
                {
                    var mo = m_Snapshot.managedObjects[m_ManagedObjectArrayIndex];
                    return mo.size;
                }

                return 0;
            }
        }

        public RichManagedType type
        {
            get
            {
                if (isValid)
                {
                    var mo = m_Snapshot.managedObjects[m_ManagedObjectArrayIndex];
                    return new RichManagedType(m_Snapshot, mo.managedTypesArrayIndex);
                }

                return RichManagedType.invalid;
            }
        }

        public RichGCHandle gcHandle
        {
            get
            {
                if (isValid)
                {
                    var mo = m_Snapshot.managedObjects[m_ManagedObjectArrayIndex];
                    if (mo.gcHandlesArrayIndex >= 0)
                        return new RichGCHandle(m_Snapshot, mo.gcHandlesArrayIndex);
                }

                return RichGCHandle.invalid;
            }
        }

        public RichNativeObject nativeObject
        {
            get
            {
                if (isValid)
                {
                    var mo = m_Snapshot.managedObjects[m_ManagedObjectArrayIndex];
                    if (mo.nativeObjectsArrayIndex >= 0)
                        return new RichNativeObject(m_Snapshot, mo.nativeObjectsArrayIndex);
                }

                return RichNativeObject.invalid;
            }
        }

        public override string ToString()
        {
            return string.Format("Valid: {0}, Addr: {1:X}, Type: {2}", isValid, address, type.name);
        }

        public static readonly RichManagedObject invalid = new RichManagedObject()
        {
            m_Snapshot = null,
            m_ManagedObjectArrayIndex = -1
        };

        PackedMemorySnapshot m_Snapshot;
        int m_ManagedObjectArrayIndex;
    }
}
