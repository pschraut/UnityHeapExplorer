using System;

namespace HeapExplorer.Utilities {
  /// <summary>
  /// Holds either the `Left` value of type <see cref="A"/> or the `Right` value of type <see cref="B"/>, but not the
  /// both at the same type.
  /// </summary>
  public readonly struct Either<A, B> {
    public readonly A __unsafeLeft;
    public readonly B __unsafeRight;
    public readonly bool isRight;
    public bool isLeft => !isRight;

    public Either(A value) {
      __unsafeLeft = value;
      __unsafeRight = default;
      isRight = false;
    }
  
    public Either(B value) {
      __unsafeLeft = default;
      __unsafeRight = value;
      isRight = true;
    }

    public R fold<R>(Func<A, R> onLeft, Func<B, R> onRight) =>
      isRight ? onRight(__unsafeRight) : onLeft(__unsafeLeft);

    /// <summary>Right-biased value extractor.</summary>
    public bool valueOut(out B o) {
      o = __unsafeRight;
      return isRight;
    }
    
    public B rightOrThrow => 
      isRight ? __unsafeRight : throw new InvalidOperationException($"Either is Left({__unsafeLeft})");

    public Option<A> leftOption => isLeft ? Option.Some(__unsafeLeft) : Option<A>.None;
    public Option<B> rightOption => isRight ? Option.Some(__unsafeRight) : Option<B>.None;
  }
}