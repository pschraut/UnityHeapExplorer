using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

namespace HeapExplorer
{
    /// <summary>
    /// The RichManagedType provides high-level access to a managed type in a memory snapshot.
    /// </summary>
    public struct RichManagedType
    {
        public RichManagedType(PackedMemorySnapshot snapshot, int managedTypesArrayIndex)
            : this()
        {
            m_snapshot = snapshot;
            m_managedTypesArrayIndex = managedTypesArrayIndex;
        }

        /// <summary>
        /// Gets the underlaying low-level type.
        /// </summary>
        public PackedManagedType packed
        {
            get
            {
                if (!isValid)
                    return new PackedManagedType() { baseOrElementTypeIndex = -1, managedTypesArrayIndex = -1 };

                return m_snapshot.managedTypes[m_managedTypesArrayIndex];
            }
        }

        public PackedMemorySnapshot snapshot
        {
            get
            {
                return m_snapshot;
            }
        }

        /// <summary>
        /// Gets whether the RichManagedType instance is valid. 
        /// </summary>
        public bool isValid
        {
            get
            {
                return m_snapshot != null && m_managedTypesArrayIndex >= 0 && m_managedTypesArrayIndex < m_snapshot.managedTypes.Length;
            }
        }

        /// <summary>
        /// Gets the name of the type.
        /// </summary>
        public string name
        {
            get
            {
                if (!isValid)
                    return "<unknown type>";

                return m_snapshot.managedTypes[m_managedTypesArrayIndex].name;
            }
        }

        /// <summary>
        /// Gets the name of the assembly where this type is stored in.
        /// </summary>
        public string assemblyName
        {
            get
            {
                if (!isValid)
                    return "<unknown assembly>";

                return m_snapshot.managedTypes[m_managedTypesArrayIndex].assembly;
            }
        }

        public bool FindField(string name, out PackedManagedField packedManagedField)
        {
            packedManagedField = new PackedManagedField();
            if (!isValid)
                return false;

            var guard = 0;
            var me = m_snapshot.managedTypes[m_managedTypesArrayIndex];
            while (me.managedTypesArrayIndex != -1)
            {
                for (var n=0; n<me.fields.Length; ++n)
                {
                    if (me.fields[n].name == name)
                    {
                        packedManagedField = me.fields[n];
                        return true;
                    }
                }

                if (++guard > 64)
                    break;

                if (me.baseOrElementTypeIndex == -1)
                    break;

                me = m_snapshot.managedTypes[me.baseOrElementTypeIndex];
            }
            return false;
        }

        /// <summary>
        /// Gets whether this native type is a subclass of the specified type 't'.
        /// </summary>
        public bool IsSubclassOf(PackedManagedType t)
        {
            if (!isValid || t.managedTypesArrayIndex == -1)
                return false;

            var me = m_snapshot.managedTypes[m_managedTypesArrayIndex];
            if (me.managedTypesArrayIndex == t.managedTypesArrayIndex)
                return true;

            var guard = 0;
            while (me.baseOrElementTypeIndex != -1)
            {
                if (++guard > 64)
                    break; // no inheritance should have more depths than this

                if (me.baseOrElementTypeIndex == t.managedTypesArrayIndex)
                    return true;

                me = m_snapshot.managedTypes[me.baseOrElementTypeIndex];
            }

            return false;
        }

        /// <summary>
        /// Gets an invalid managed type instance.
        /// </summary>
        public static readonly RichManagedType invalid = new RichManagedType()
        {
            m_snapshot = null,
            m_managedTypesArrayIndex = -1
        };

        PackedMemorySnapshot m_snapshot;
        int m_managedTypesArrayIndex;
    }
}
