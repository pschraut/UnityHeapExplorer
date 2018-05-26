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
            m_snapshot = snapshot;
            m_managedStaticFieldsArrayIndex = staticFieldsArrayIndex;
            m_isValid = m_snapshot != null && m_managedStaticFieldsArrayIndex >= 0 && m_managedStaticFieldsArrayIndex < m_snapshot.managedStaticFields.Length;
        }

        public PackedManagedStaticField packed
        {
            get
            {
                if (!m_isValid)
                    return PackedManagedStaticField.New();

                return m_snapshot.managedStaticFields[m_managedStaticFieldsArrayIndex];
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

        public System.Int32 arrayIndex
        {
            get
            {
                return m_managedStaticFieldsArrayIndex;
            }
        }

        public RichManagedType fieldType
        {
            get
            {
                if (m_isValid)
                {
                    var mo = m_snapshot.managedStaticFields[m_managedStaticFieldsArrayIndex];

                    var staticClassType = m_snapshot.managedTypes[mo.managedTypesArrayIndex];
                    var staticField = staticClassType.fields[mo.fieldIndex];
                    var staticFieldType = m_snapshot.managedTypes[staticField.managedTypesArrayIndex];

                    return new RichManagedType(m_snapshot, staticFieldType.managedTypesArrayIndex);
                }

                return RichManagedType.invalid;
            }
        }

        public RichManagedType classType
        {
            get
            {
                if (m_isValid)
                {
                    var mo = m_snapshot.managedStaticFields[m_managedStaticFieldsArrayIndex];
                    return new RichManagedType(m_snapshot, mo.managedTypesArrayIndex);
                }

                return RichManagedType.invalid;
            }
        }

        public static readonly RichStaticField invalid = new RichStaticField()
        {
            m_snapshot = null,
            m_managedStaticFieldsArrayIndex = -1,
            m_isValid = false,
        };

        PackedMemorySnapshot m_snapshot;
        int m_managedStaticFieldsArrayIndex;
        bool m_isValid;
    }
}
