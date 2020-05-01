//
// Heap Explorer for Unity. Copyright (c) 2019 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://bitbucket.org/pschraut/unityheapexplorer/
//
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
            m_Snapshot = snapshot;
            m_NativeObjectArrayIndex = nativeObjectsArrayIndex;
        }

        public PackedNativeUnityEngineObject packed
        {
            get
            {
                if (!isValid)
                    return new PackedNativeUnityEngineObject() { nativeObjectsArrayIndex = -1, nativeTypesArrayIndex = -1, managedObjectsArrayIndex = -1 };

                return m_Snapshot.nativeObjects[m_NativeObjectArrayIndex];
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
                return m_Snapshot != null && m_NativeObjectArrayIndex >= 0 && m_NativeObjectArrayIndex < m_Snapshot.nativeObjects.Length;
            }
        }
        
        public RichNativeType type
        {
            get
            {
                if (!isValid)
                    return RichNativeType.invalid;

                var obj = m_Snapshot.nativeObjects[m_NativeObjectArrayIndex];
                return new RichNativeType(m_Snapshot, obj.nativeTypesArrayIndex);
            }
        }

        public RichManagedObject managedObject
        {
            get
            {
                if (!isValid)
                    return RichManagedObject.invalid;

                var native = m_Snapshot.nativeObjects[m_NativeObjectArrayIndex];
                if (native.managedObjectsArrayIndex < 0)
                    return RichManagedObject.invalid;

                return new RichManagedObject(m_Snapshot, native.managedObjectsArrayIndex);
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

                return m_Snapshot.nativeObjects[m_NativeObjectArrayIndex].name;
            }
        }

        public System.UInt64 address
        {
            get
            {
                if (!isValid)
                    return 0;

                return (System.UInt64)m_Snapshot.nativeObjects[m_NativeObjectArrayIndex].nativeObjectAddress;
            }
        }

        public HideFlags hideFlags
        {
            get
            {
                if (!isValid)
                    return HideFlags.None;

                return m_Snapshot.nativeObjects[m_NativeObjectArrayIndex].hideFlags;
            }
        }

        public int instanceId
        {
            get
            {
                if (!isValid)
                    return 0;

                return m_Snapshot.nativeObjects[m_NativeObjectArrayIndex].instanceId;
            }
        }

        public bool isDontDestroyOnLoad
        {
            get
            {
                if (!isValid)
                    return true;

                return m_Snapshot.nativeObjects[m_NativeObjectArrayIndex].isDontDestroyOnLoad;
            }
        }

        public bool isManager
        {
            get
            {
                if (!isValid)
                    return false;

                return m_Snapshot.nativeObjects[m_NativeObjectArrayIndex].isManager;
            }
        }

        public bool isPersistent
        {
            get
            {
                if (!isValid)
                    return false;

                return m_Snapshot.nativeObjects[m_NativeObjectArrayIndex].isPersistent;
            }
        }

        public int size
        {
            get
            {
                if (!isValid)
                    return 0;

                return m_Snapshot.nativeObjects[m_NativeObjectArrayIndex].size;
            }
        }

        public void GetConnections(List<PackedConnection> references, List<PackedConnection> referencedBy)
        {
            if (!isValid)
                return;

            m_Snapshot.GetConnections(packed, references, referencedBy);
        }

        public void GetConnectionsCount(out int referencesCount, out int referencedByCount)
        {
            if (!isValid)
            {
                referencesCount = 0;
                referencedByCount = 0;
                return;
            }

            m_Snapshot.GetConnectionsCount(PackedConnection.Kind.Native, m_NativeObjectArrayIndex, out referencesCount, out referencedByCount);
        }

        public static readonly RichNativeObject invalid = new RichNativeObject()
        {
            m_Snapshot = null,
            m_NativeObjectArrayIndex = -1
        };

        PackedMemorySnapshot m_Snapshot;
        int m_NativeObjectArrayIndex;
    }
}
