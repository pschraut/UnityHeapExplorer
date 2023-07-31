namespace HeapExplorer.Utilities {
  /// <summary>
  /// A type that carries no information (similarly to `void`) but you can use it in generic type/method definitions.
  /// </summary>
  public readonly struct Unit {
    public static Unit _ => new Unit();
  }
}