using System;
using HeapExplorer.Utilities;

namespace HeapExplorer {
  public partial struct PackedManagedObject {
    /// <summary>
    /// Named tuple of <see cref="isStatic"/> and <see cref="index"/>.
    /// </summary>
    public readonly struct ArrayIndex : IEquatable<ArrayIndex> {
      /// <summary>Is this a reference to a static field?</summary>
      public readonly bool isStatic;

      /// <summary>
      /// An index into the <see cref="PackedMemorySnapshot.managedObjects"/> array or
      /// <see cref="PackedMemorySnapshot.managedStaticFields"/> array that stores this managed
      /// object.
      /// </summary>
      public readonly PInt index;

      public ArrayIndex(bool isStatic, PInt index) {
        this.isStatic = isStatic;
        this.index = index;
      }

      /// <summary>Indexes into <see cref="PackedMemorySnapshot.managedStaticFields"/>.</summary>
      public static ArrayIndex newStatic(PInt index) => new ArrayIndex(isStatic: true, index);

      /// <summary>Indexes into <see cref="PackedMemorySnapshot.managedObjects"/>.</summary>
      public static ArrayIndex newObject(PInt index) => new ArrayIndex(isStatic: false, index);

      public override string ToString() {
        var staticStr = isStatic ? ", for static field" : "";
        return $"ManagedObjectIndex({index}{staticStr})";
      }

      public PackedConnection.Pair asPair =>
        new PackedConnection.Pair(
          isStatic ? PackedConnection.Kind.StaticField : PackedConnection.Kind.Managed,
          index
        );

      #region Equality
      public bool Equals(ArrayIndex other) => isStatic == other.isStatic && index == other.index;
      public override bool Equals(object obj) => obj is ArrayIndex other && Equals(other);
      public static bool operator ==(ArrayIndex left, ArrayIndex right) => left.Equals(right);
      public static bool operator !=(ArrayIndex left, ArrayIndex right) => !left.Equals(right);

      public override int GetHashCode() {
        unchecked {
          return (isStatic.GetHashCode() * 397) ^ index;
        }
      }
      #endregion
    }
  }
}