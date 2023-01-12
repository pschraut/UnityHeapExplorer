using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HeapExplorer.Utilities {
  /// <summary>
  /// Allows you to track if you're in a cycle.
  /// </summary>
  /// <typeparam name="A">Type of the item we're tracking.</typeparam>
  /// <example><code><![CDATA[
  /// var cycleTracker = new CycleTracker<int>();
  /// do {
  ///   if (cycleTracker.markIteration(type.managedTypesArrayIndex)) {
  ///     cycleTracker.report(
  ///       $"{nameof(HasTypeOrBaseAnyField)}()", type.managedTypesArrayIndex,
  ///       idx => snapshot.managedTypes[idx].ToString()
  ///     );
  ///     break;
  ///   }
  ///
  ///   // your logic
  /// } while (/* your condition */)
  /// ]]></code></example>
  public class CycleTracker<A> {
    /// <summary>
    /// Marks seen items and the depth (number of <see cref="markIteration"/> invocations) at which we have seen them.
    /// </summary>
    public readonly Dictionary<A, int> itemToDepth = new Dictionary<A, int>();

    const int INITIAL_DEPTH = -1;
    int depth = INITIAL_DEPTH;

    /// <summary>
    /// Prepares for iterating.
    /// </summary>
    public void markStartOfSearch() {
      itemToDepth.Clear();
      depth = INITIAL_DEPTH;
    }

    /// <summary>
    /// Marks one iteration in which we see <see cref="item"/>.
    /// </summary>
    /// <returns>true if a cycle has been detected</returns>
    public bool markIteration(A item) {
      depth++;
      if (itemToDepth.ContainsKey(item)) {
        return true;
      }
      else {
        itemToDepth.Add(item, depth);
        return false;
      }
    }
    
    /// <summary>
    /// Reports that a cycle has occured to the Unity console.
    /// </summary>
    /// <param name="description">Description of the invoking method.</param>
    /// <param name="item">The item on which the cycle occured.</param>
    /// <param name="itemToString">Converts items to human-readable strings.</param>
    public void reportCycle(
      string description,
      A item,
      Func<A, string> itemToString
    ) {
      var itemsStr = string.Join("\n", 
        itemToDepth.OrderBy(kv => kv.Value).Select(kv => $"[{kv.Value:D3}] {itemToString(kv.Key)}")
      );
      var text = 
        $"HeapExplorer: hit cycle guard at depth {itemToDepth.Count + 1} in {description} for {itemToString(item)}:\n"
        + itemsStr;
      Debug.LogWarning(text);
    }
  }
}