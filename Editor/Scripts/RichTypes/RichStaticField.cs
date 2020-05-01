//
// Heap Explorer for Unity. Copyright (c) 2019 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://bitbucket.org/pschraut/unityheapexplorer/
//
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HeapExplorer
{
    public struct RichStaticField
    {
        public RichStaticField(PackedMemorySnapshot snapshot, int staticFieldsArrayIndex)
            : this()
        {
            m_Snapshot = snapshot;
            m_ManagedStaticFieldsArrayIndex = staticFieldsArrayIndex;
        }

        public PackedManagedStaticField packed
        {
            get
            {
                if (!isValid)
                {
                    return new PackedManagedStaticField()
                    {
                        managedTypesArrayIndex = -1,
                        fieldIndex = -1,
                        staticFieldsArrayIndex = -1,
                    };
                }

                return m_Snapshot.managedStaticFields[m_ManagedStaticFieldsArrayIndex];
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
                var value = m_Snapshot != null && m_ManagedStaticFieldsArrayIndex >= 0 && m_ManagedStaticFieldsArrayIndex < m_Snapshot.managedStaticFields.Length;
                return value;
            }
        }

        public System.Int32 arrayIndex
        {
            get
            {
                return m_ManagedStaticFieldsArrayIndex;
            }
        }

        public RichManagedType fieldType
        {
            get
            {
                if (isValid)
                {
                    var mo = m_Snapshot.managedStaticFields[m_ManagedStaticFieldsArrayIndex];

                    var staticClassType = m_Snapshot.managedTypes[mo.managedTypesArrayIndex];
                    var staticField = staticClassType.fields[mo.fieldIndex];
                    var staticFieldType = m_Snapshot.managedTypes[staticField.managedTypesArrayIndex];

                    return new RichManagedType(m_Snapshot, staticFieldType.managedTypesArrayIndex);
                }

                return RichManagedType.invalid;
            }
        }

        public RichManagedType classType
        {
            get
            {
                if (isValid)
                {
                    var mo = m_Snapshot.managedStaticFields[m_ManagedStaticFieldsArrayIndex];
                    return new RichManagedType(m_Snapshot, mo.managedTypesArrayIndex);
                }

                return RichManagedType.invalid;
            }
        }

        public static readonly RichStaticField invalid = new RichStaticField()
        {
            m_Snapshot = null,
            m_ManagedStaticFieldsArrayIndex = -1
        };

        PackedMemorySnapshot m_Snapshot;
        int m_ManagedStaticFieldsArrayIndex;
    }
}
