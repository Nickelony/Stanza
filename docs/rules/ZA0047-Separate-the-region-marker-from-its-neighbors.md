# ZA0047 - Separate the region marker from its neighbors

**Suggestion, on by default.**
Writes a blank line on each side of a `#region` or an `#endregion` directive,
except beside a brace of the block that holds it, or another directive.

```csharp
// Before
class C
{
	private int _total;
	#region Fields
	private int _count;
	#endregion
	private int _sum;
}

// After
class C
{
	private int _total;

	#region Fields

	private int _count;

	#endregion

	private int _sum;
}
```

- A marker beside another directive gets no blank line, so adjacent region directives, such as the two markers of an empty region, stay tight.
- A comment directly above a marker moves with it, so the blank line goes above the comment.
- This rule has no tight counterpart; disable it to keep region markers tight.
