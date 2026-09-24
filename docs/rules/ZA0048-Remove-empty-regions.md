# ZA0048 - Remove empty regions

**Suggestion, off by default.**
Removes a region that holds nothing but whitespace or further empty regions,
together with its two markers.

```csharp
// Before
class C
{
	private int _total;

	#region Fields
	#endregion

	private int _sum;
}

// After
class C
{
	private int _total;

	private int _sum;
}
```

- A region that holds a comment is left alone.
- A region whose nested region holds code is left alone.
- Nested empty regions, and empty regions separated only by blank lines, form one run.
  One diagnostic removes the whole run.
- The blank lines around the run go with it;
  one stays only if a blank line stood beside the run and a line remains on both sides.
