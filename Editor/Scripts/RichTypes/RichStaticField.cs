//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//

using System;

namespace HeapExplorer
{
    /// <summary>
    /// An <see cref="PackedManagedStaticField"/> index validated against a <see cref="PackedMemorySnapshot"/>.
    /// </summary>
    public readonly struct RichStaticField
    {
        public RichStaticField(PackedMemorySnapshot snapshot, int staticFieldsArrayIndex) {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (staticFieldsArrayIndex < 0 || staticFieldsArrayIndex >= snapshot.managedStaticFields.Length)
                throw new ArgumentOutOfRangeException(
                    $"staticFieldsArrayIndex ({staticFieldsArrayIndex}) is out of bounds [0..{snapshot.managedStaticFields.Length})"
                );

            
            this.snapshot = snapshot;
            managedStaticFieldsArrayIndex = staticFieldsArrayIndex;
        }

        public override string ToString() =>
            $"Name: {staticField.name}, In Type: {classType.name}, Of Type: {fieldType.name}";

        public PackedManagedStaticField packed => snapshot.managedStaticFields[managedStaticFieldsArrayIndex];

        public PackedManagedField staticField {
            get {
                var mo = packed;

                var staticClassType = snapshot.managedTypes[mo.managedTypesArrayIndex];
                var staticField = staticClassType.fields[mo.fieldIndex];
                return staticField;
            }
        }

        public RichManagedType fieldType {
            get {
                var staticFieldType = snapshot.managedTypes[staticField.managedTypesArrayIndex];
                return new RichManagedType(snapshot, staticFieldType.managedTypesArrayIndex);
            }
        }

        public RichManagedType classType =>
            new RichManagedType(snapshot, packed.managedTypesArrayIndex);

        public readonly PackedMemorySnapshot snapshot;
        public readonly int managedStaticFieldsArrayIndex;
    }
}
