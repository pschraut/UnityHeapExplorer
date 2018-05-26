using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HeapExplorer
{
    public struct RichNativeObject
    {
        public RichNativeObject(PackedMemorySnapshot snapshot, int nativeObjectsArrayIndex)
            : this()
        {
            m_snapshot = snapshot;
            m_nativeObjectArrayIndex = nativeObjectsArrayIndex;
        }

        public PackedNativeUnityEngineObject packed
        {
            get
            {
                if (!isValid)
                    return new PackedNativeUnityEngineObject() { nativeObjectsArrayIndex = -1, nativeTypesArrayIndex = -1, managedObjectsArrayIndex = -1 };

                return m_snapshot.nativeObjects[m_nativeObjectArrayIndex];
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
                return m_snapshot != null && m_nativeObjectArrayIndex >= 0 && m_nativeObjectArrayIndex < m_snapshot.nativeObjects.Length;
            }
        }
        
        public RichNativeType type
        {
            get
            {
                if (!isValid)
                    return RichNativeType.invalid;

                var obj = m_snapshot.nativeObjects[m_nativeObjectArrayIndex];
                return new RichNativeType(m_snapshot, obj.nativeTypesArrayIndex);
            }
        }

        public RichManagedObject managedObject
        {
            get
            {
                if (!isValid)
                    return RichManagedObject.invalid;

                var native = m_snapshot.nativeObjects[m_nativeObjectArrayIndex];
                if (native.managedObjectsArrayIndex < 0)
                    return RichManagedObject.invalid;

                return new RichManagedObject(m_snapshot, native.managedObjectsArrayIndex);
            }
        }

        public RichGCHandle gcHandle
        {
            get
            {
                return managedObject.gcHandle;
            }
        }

        public string name
        {
            get
            {
                if (!isValid)
                    return "<invalid>";

                return m_snapshot.nativeObjects[m_nativeObjectArrayIndex].name;
            }
        }

        public System.UInt64 address
        {
            get
            {
                if (!isValid)
                    return 0;

                return (System.UInt64)m_snapshot.nativeObjects[m_nativeObjectArrayIndex].nativeObjectAddress;
            }
        }

        public HideFlags hideFlags
        {
            get
            {
                if (!isValid)
                    return HideFlags.None;

                return m_snapshot.nativeObjects[m_nativeObjectArrayIndex].hideFlags;
            }
        }

        public int instanceId
        {
            get
            {
                if (!isValid)
                    return 0;

                return m_snapshot.nativeObjects[m_nativeObjectArrayIndex].instanceId;
            }
        }

        public bool isDontDestroyOnLoad
        {
            get
            {
                if (!isValid)
                    return true;

                return m_snapshot.nativeObjects[m_nativeObjectArrayIndex].isDontDestroyOnLoad;
            }
        }

        public bool isManager
        {
            get
            {
                if (!isValid)
                    return false;

                return m_snapshot.nativeObjects[m_nativeObjectArrayIndex].isManager;
            }
        }

        public bool isPersistent
        {
            get
            {
                if (!isValid)
                    return false;

                return m_snapshot.nativeObjects[m_nativeObjectArrayIndex].isPersistent;
            }
        }

        public int size
        {
            get
            {
                if (!isValid)
                    return 0;

                return m_snapshot.nativeObjects[m_nativeObjectArrayIndex].size;
            }
        }

        public void GetConnections(List<PackedConnection> references, List<PackedConnection> referencedBy)
        {
            if (!isValid)
                return;

            m_snapshot.GetConnections(packed, references, referencedBy);
        }

        public void GetConnectionsCount(out int referencesCount, out int referencedByCount)
        {
            if (!isValid)
            {
                referencesCount = 0;
                referencedByCount = 0;
                return;
            }

            m_snapshot.GetConnectionsCount(PackedConnection.Kind.Native, m_nativeObjectArrayIndex, out referencesCount, out referencedByCount);
        }

        public static readonly RichNativeObject invalid = new RichNativeObject()
        {
            m_snapshot = null,
            m_nativeObjectArrayIndex = -1
        };

        PackedMemorySnapshot m_snapshot;
        int m_nativeObjectArrayIndex;
    }
}
