using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using static HeapExplorer.Utilities.Option;

namespace HeapExplorer.Utilities {
  /// <summary>
  /// Positive integer.
  /// <para/>
  /// Similar to <see cref="uint"/> but is still backed by an <see cref="int"/> so the math behaves the same.
  /// </summary>
  public readonly struct PInt : IEquatable<PInt> {
    public readonly int asInt;

    PInt(int asInt) {
      this.asInt = asInt;
    }

    public static PInt _0 => new PInt(0);
    public static PInt _1 => new PInt(1);

    public override string ToString() => asInt.ToString();

    /// <summary>Safely casts the <see cref="int"/> value to <see cref="uint"/>.</summary>
    public uint asUInt {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => (uint) asInt;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PInt operator +(PInt p1, PInt p2) => new PInt(p1.asInt + p2.asInt);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PInt operator ++(PInt p1) => new PInt(p1.asInt + 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator int(PInt pInt) => pInt.asInt;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator long(PInt pInt) => pInt.asInt;
  
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator uint(PInt pInt) => pInt.asUInt;
  
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ulong(PInt pInt) => pInt.asUInt;
  
    /// <summary>Creates a value, throwing if the supplied <see cref="int"/> is negative.</summary>
    public static PInt createOrThrow(int value) {
      if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), value, "value can't be negative");
      return new PInt(value);
    }
  
    /// <summary>Creates a value, returning `None` if the supplied <see cref="int"/> is negative.</summary>
    public static Option<PInt> create(int value) => 
      value < 0 ? new Option<PInt>() : Some(new PInt(value));
  
    /// <summary>Creates a value, returning `Left(value)` if the supplied <see cref="int"/> is negative.</summary>
    public static Either<int, PInt> createEither(int value) => 
      value < 0 ? new Either<int, PInt>(value) : new Either<int, PInt>(new PInt(value));

    #region Equality
  
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(PInt other) => asInt == other.asInt;
  
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object obj) => obj is PInt other && Equals(other);
  
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => asInt;
  
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(PInt left, PInt right) => left.Equals(right);
  
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(PInt left, PInt right) => !left.Equals(right);

    #endregion
  }

  public static class PIntExts {
    /// <summary><see cref="Array.Length"/> as <see cref="PInt"/>.</summary>
    public static PInt LengthP<A>(this A[] array) => PInt.createOrThrow(array.Length);
    
    /// <summary><see cref="ICollection{T}.Count"/> as <see cref="PInt"/>.</summary>
    public static PInt CountP<A>(this ICollection<A> list) => PInt.createOrThrow(list.Count);

    public static PInt ReadPInt(this BinaryReader reader) => PInt.createOrThrow(reader.ReadInt32());
  }
}