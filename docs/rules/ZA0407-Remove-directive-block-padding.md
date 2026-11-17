# ZA0407 - Remove directive block padding

**Suggestion, on by default.**
Removes the blank lines directly inside the markers of a region, a conditional block or a suppression block whose padding key says `tight`,
below the opening marker and above the closing one.

```csharp
// Before
var options = Load();

#pragma warning disable CS0618

var legacy = options.OldSetting;

#pragma warning restore CS0618

Apply(legacy);

// After
var options = Load();

#pragma warning disable CS0618
var legacy = options.OldSetting;
#pragma warning restore CS0618

Apply(legacy);
```

A conditional block loses the blank lines on both sides of an `#elif` and an `#else` as well:

```csharp
// Before
private int _total;

#if TRACE

private int _trace;

#else

private int _other;

#endif

private int _sum;

// After
private int _total;

#if TRACE
private int _trace;
#else
private int _other;
#endif

private int _sum;
```

Regions are padded by default, so a region keeps its inside until its key says `tight`:

```csharp
// Before
#region Fields

private int _count;

#endregion

// After - with tight regions
#region Fields
private int _count;
#endregion
```

- Each kind reads its own key: [`stanza_region_padding`](https://github.com/Nickelony/Stanza/wiki/stanza_region_padding) defaults to `padded`,
  and [`stanza_conditional_block_padding`](https://github.com/Nickelony/Stanza/wiki/stanza_conditional_block_padding) and [`stanza_suppression_block_padding`](https://github.com/Nickelony/Stanza/wiki/stanza_suppression_block_padding) default to `tight`.
- The separating rules read the same keys, so a kind is padded by [`ZA0401`](https://github.com/Nickelony/Stanza/wiki/ZA0401-Separate-the-region-marker-from-its-neighbors),
  [`ZA0402`](https://github.com/Nickelony/Stanza/wiki/ZA0402-Separate-the-conditional-block-from-its-neighbors) or [`ZA0406`](https://github.com/Nickelony/Stanza/wiki/ZA0406-Separate-a-suppression-block), or tightened by this rule, and no rule contests a gap with another.
- This rule reads only the keys, so it removes the blank lines of a `tight` kind whether or not the separating rule is on.
- The blank lines outside the block stay, because the separating rule owns them.
- A gap that holds a comment, a documentation comment included, stays as written, and so does a gap beside another directive.
- Only a paired block in active code is judged, and a conditional block or a suppression block must stand between units
  as [`ZA0402`](https://github.com/Nickelony/Stanza/wiki/ZA0402-Separate-the-conditional-block-from-its-neighbors) and [`ZA0406`](https://github.com/Nickelony/Stanza/wiki/ZA0406-Separate-a-suppression-block) require.
  A `disable` with no `restore`, a block inside an argument list and a directive in disabled text keep their gaps.
- An empty region belongs to [`ZA0405`](https://github.com/Nickelony/Stanza/wiki/ZA0405-Remove-empty-regions), which removes it with the blank lines around it.
- Every blank line of the gap goes, so [`ZA0056`](https://github.com/Nickelony/Stanza/wiki/ZA0056-Collapse-extra-blank-lines) has nothing to cap there.
- CSharpier never writes these blank lines, so the removal agrees with it, as [Working with formatters](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#layout-that-agrees-with-csharpier) says.
