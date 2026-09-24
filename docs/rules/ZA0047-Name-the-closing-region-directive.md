# ZA0047 - Name the closing region directive

**Suggestion, off by default.**
Copies the whole label of an opening `#region`, such as `Fields (mutable)`,
to its `#endregion`.

```csharp
// Before
#region Fields (mutable)
private int _count;
#endregion

// After
#region Fields (mutable)
private int _count;
#endregion Fields (mutable)
```

- An unnamed region is left alone.
- A different label on the `#endregion` is replaced.
- A `//` note on the `#region` line is copied with the label.
- A closing marker with a `//` or `/*` comment is left alone, so the comment stays.
- Nested regions pair each `#endregion` with the innermost open `#region`.
