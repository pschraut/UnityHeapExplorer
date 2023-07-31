using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using static HeapExplorer.Utilities.Option;

namespace HeapExplorer.Utilities {
  public static class Utils {
    public static Option<A> zeroAddressAccessError<A>(string paramName) {
      // throw new ArgumentOutOfRangeException(paramName, 0, "address 0 should not be accessed!");
      Debug.LogError("HeapExplorer: address 0 should not be accessed!");
      return new Option<A>();
    }

    public static void reportInvalidSizeError(
      PackedManagedType type, ConcurrentDictionary<string, Unit> reported
    ) {
      if (reported.TryAdd(type.name, Unit._)) {
        var size = type.size.fold(v => v, v => v);
        Debug.LogError(
          $"HeapExplorer: Unity reported invalid size {size} for type '{type.name}', this is a Unity bug! "
          + $"We can't continue the current operation because of that, data will most likely be incomplete!"
        );
      }
    }

    /// <summary>
    /// Returns the <see cref="uint"/> value as <see cref="int"/>, clamping it if it doesn't fit. 
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ToIntClamped(this uint value, bool doLog = true) {
      if (value > int.MaxValue) {
        if (doLog)
          Debug.LogWarningFormat(
            "HeapExplorer: clamping uint value {0} to int value {1}, this shouldn't happen.",
            value, int.MaxValue
          );
        return int.MaxValue;
      }

      return (int) value;
    }

    /// <summary>
    /// Returns the <see cref="ulong"/> value as <see cref="long"/>, clamping it if it doesn't fit. 
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ToLongClamped(this ulong value, bool doLog = true) {
      if (value > long.MaxValue) {
        if (doLog)
          Debug.LogWarningFormat(
            "HeapExplorer: clamping ulong value {0} to long value {1}, this shouldn't happen.",
            value, long.MaxValue
          );
        return long.MaxValue;
      }

      return (long) value;
    }

    /// <summary>
    /// Returns the <see cref="int"/> value as <see cref="uint"/>, clamping it if it is less than 0. 
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ToUIntClamped(this int value, bool doLog = true) {
      if (value < 0) {
        if (doLog)
          Debug.LogWarningFormat(
            "HeapExplorer: clamping int value {0} to uint 0, this shouldn't happen.", value
          );
        return 0;
      }

      return (uint) value;
    }

    /// <summary>
    /// Returns the <see cref="long"/> value as <see cref="ulong"/>, clamping it if it is less than 0. 
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ToULongClamped(this long value, bool doLog = true) {
      if (value < 0) {
        if (doLog)
          Debug.LogWarningFormat(
            "HeapExplorer: clamping long value {0} to ulong 0, this shouldn't happen.", value
          );
        return 0;
      }

      return (ulong) value;
    }

    /// <summary>
    /// <see cref="IDictionary{TKey,TValue}.TryGetValue"/> but returns an <see cref="Option{A}"/> instead.
    /// </summary>
    /// <example><code><![CDATA[
    /// var maybeList = m_ConnectionsFrom.get(key);
    /// ]]></code></example>
    public static Option<V> get<K, V>(this IDictionary<K, V> dictionary, K key) => 
      dictionary.TryGetValue(key, out var value) ? Some(value) : new Option<V>();

    /// <summary>
    /// Gets a value by the key from the dictionary. If a key is not stored yet it is created using the
    /// <see cref="ifMissing"/> function and then stored in the dictionary. 
    /// </summary>
    /// <example><code><![CDATA[
    /// var list = m_ConnectionsFrom.getOrUpdate(key, _ => new List<int>());
    /// ]]></code></example>
    public static V getOrUpdate<K, V>(this IDictionary<K, V> dictionary, K key, Func<K, V> ifMissing) {
      if (!dictionary.TryGetValue(key, out var value)) {
        value = dictionary[key] = ifMissing(key);
      }

      return value;
    }

    /// <summary>
    /// Gets a value by the key from the dictionary. If a key is not stored yet it is created using the
    /// <see cref="ifMissing"/> function and then stored in the dictionary. 
    /// </summary>
    /// <example><code><![CDATA[
    /// var list = m_ConnectionsFrom.getOrUpdate(key, data, (_, data) => new List<int>());
    /// ]]></code></example>
    public static V getOrUpdate<K, Data, V>(this IDictionary<K, V> dictionary, K key, Data data, Func<K, Data, V> ifMissing) {
      if (!dictionary.TryGetValue(key, out var value)) {
        value = dictionary[key] = ifMissing(key, data);
      }

      return value;
    }

    /// <summary>
    /// Gets a value by the key from the dictionary. If a key is not stored yet it returns <see cref="ifMissing"/>
    /// instead.
    /// </summary>
    /// <example><code><![CDATA[
    /// var list = m_ConnectionsFrom.getOrUpdate(key, _ => new List<int>());
    /// ]]></code></example>
    public static V getOrElse<K, V>(this IDictionary<K, V> dictionary, K key, V ifMissing) => 
      dictionary.TryGetValue(key, out var value) ? value : ifMissing;

    /// <summary>
    /// Groups entries into groups of <see cref="groupSize"/>.
    /// </summary>
    /// <example><code><![CDATA[
    /// [1, 2, 3, 4, 5].groupedIn(2) == [[1, 2], [3, 4], [5]]
    /// ]]></code></example>
    public static IEnumerable<List<A>> groupedIn<A>(this IEnumerable<A> enumerable, PInt groupSize) {
      var group = new List<A>(groupSize);
      foreach (var a in enumerable) {
        // Yield if a group is full.
        if (group.Count == groupSize) {
          yield return group;
          group = new List<A>(groupSize);
        }
        
        group.Add(a);
      }

      // Yield the last group, which may not be full.
      yield return group;
    }
  }
}