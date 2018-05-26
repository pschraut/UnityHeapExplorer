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
            m_snapshot = snapshot;
            m_managedObjectArrayIndex = managedObjectsArrayIndex;
            //m_isValid = m_snapshot != null && m_managedObjectArrayIndex >= 0 && m_managedObjectArrayIndex < m_snapshot.managedObjects.Length;
        }

        public PackedManagedObject packed
        {
            get
            {
                if (!isValid)
                    return PackedManagedObject.New();

                return m_snapshot.managedObjects[m_managedObjectArrayIndex];
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
                var value = m_snapshot != null && m_managedObjectArrayIndex >= 0 && m_managedObjectArrayIndex < m_snapshot.managedObjects.Length;
                return value;
                //return m_isValid;
            }
        }

        public System.Int32 arrayIndex
        {
            get
            {
                return m_managedObjectArrayIndex;
            }
        }

        public System.UInt64 address
        {
            get
            {
                if (!isValid)
                    return 0;

                var mo = m_snapshot.managedObjects[m_managedObjectArrayIndex];
                return mo.address;
            }
        }

        public System.Int32 size
        {
            get
            {
                if (isValid)
                {
                    var mo = m_snapshot.managedObjects[m_managedObjectArrayIndex];
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
                    var mo = m_snapshot.managedObjects[m_managedObjectArrayIndex];
                    return new RichManagedType(m_snapshot, mo.managedTypesArrayIndex);
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
                    var mo = m_snapshot.managedObjects[m_managedObjectArrayIndex];
                    if (mo.gcHandlesArrayIndex >= 0)
                        return new RichGCHandle(m_snapshot, mo.gcHandlesArrayIndex);
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
                    var mo = m_snapshot.managedObjects[m_managedObjectArrayIndex];
                    if (mo.nativeObjectsArrayIndex >= 0)
                        return new RichNativeObject(m_snapshot, mo.nativeObjectsArrayIndex);
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
            m_snapshot = null,
            m_managedObjectArrayIndex = -1,
            //m_isValid = false,
        };

        PackedMemorySnapshot m_snapshot;
        int m_managedObjectArrayIndex;
        //bool m_isValid;
    }
}
