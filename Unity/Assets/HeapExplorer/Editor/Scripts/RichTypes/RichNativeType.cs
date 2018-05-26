using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace HeapExplorer
{
    public struct RichNativeType
    {
        public RichNativeType(PackedMemorySnapshot snapshot, int nativeTypesArrayIndex)
            : this()
        {
            m_snapshot = snapshot;
            m_nativeTypesArrayIndex = nativeTypesArrayIndex;
        }

        public PackedNativeType packed
        {
            get
            {
                if (!isValid)
                    return new PackedNativeType() { nativeTypeArrayIndex = -1, nativeBaseTypeArrayIndex = -1, name = "" };

                return m_snapshot.nativeTypes[m_nativeTypesArrayIndex];
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
                return m_snapshot != null && m_nativeTypesArrayIndex >= 0 && m_nativeTypesArrayIndex < m_snapshot.nativeTypes.Length;
            }
        }

        public string name
        {
            get
            {
                if (!isValid)
                    return "<unknown type>";

                var t = m_snapshot.nativeTypes[m_nativeTypesArrayIndex];
                return t.name;
            }
        }

        public RichNativeType baseType
        {
            get
            {
                if (!isValid)
                    return RichNativeType.invalid;

                var t = m_snapshot.nativeTypes[m_nativeTypesArrayIndex];
                if (t.nativeBaseTypeArrayIndex < 0)
                    return RichNativeType.invalid;

                return new RichNativeType(m_snapshot, t.nativeBaseTypeArrayIndex);
            }
        }

        /// <summary>
        /// Gets whether this native type is a subclass of the specified baseType.
        /// </summary>
        public bool IsSubclassOf(int baseTypeIndex)
        {
            if (!isValid || baseTypeIndex < 0)
                return false;

            return m_snapshot.IsSubclassOf(m_snapshot.nativeTypes[m_nativeTypesArrayIndex], baseTypeIndex);
        }

        public static readonly RichNativeType invalid = new RichNativeType()
        {
            m_snapshot = null,
            m_nativeTypesArrayIndex = -1
        };

        PackedMemorySnapshot m_snapshot;
        int m_nativeTypesArrayIndex;
    }
}
