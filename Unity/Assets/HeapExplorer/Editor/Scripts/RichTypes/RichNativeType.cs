//
// Heap Explorer for Unity. Copyright (c) 2019 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://bitbucket.org/pschraut/unityheapexplorer/
//
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
            m_Snapshot = snapshot;
            m_NativeTypesArrayIndex = nativeTypesArrayIndex;
        }

        public PackedNativeType packed
        {
            get
            {
                if (!isValid)
                    return new PackedNativeType() { nativeTypeArrayIndex = -1, nativeBaseTypeArrayIndex = -1, name = "" };

                return m_Snapshot.nativeTypes[m_NativeTypesArrayIndex];
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
                return m_Snapshot != null && m_NativeTypesArrayIndex >= 0 && m_NativeTypesArrayIndex < m_Snapshot.nativeTypes.Length;
            }
        }

        public string name
        {
            get
            {
                if (!isValid)
                    return "<unknown type>";

                var t = m_Snapshot.nativeTypes[m_NativeTypesArrayIndex];
                return t.name;
            }
        }

        public RichNativeType baseType
        {
            get
            {
                if (!isValid)
                    return RichNativeType.invalid;

                var t = m_Snapshot.nativeTypes[m_NativeTypesArrayIndex];
                if (t.nativeBaseTypeArrayIndex < 0)
                    return RichNativeType.invalid;

                return new RichNativeType(m_Snapshot, t.nativeBaseTypeArrayIndex);
            }
        }

        /// <summary>
        /// Gets whether this native type is a subclass of the specified baseType.
        /// </summary>
        public bool IsSubclassOf(int baseTypeIndex)
        {
            if (!isValid || baseTypeIndex < 0)
                return false;

            return m_Snapshot.IsSubclassOf(m_Snapshot.nativeTypes[m_NativeTypesArrayIndex], baseTypeIndex);
        }

        public static readonly RichNativeType invalid = new RichNativeType()
        {
            m_Snapshot = null,
            m_NativeTypesArrayIndex = -1
        };

        PackedMemorySnapshot m_Snapshot;
        int m_NativeTypesArrayIndex;
    }
}
