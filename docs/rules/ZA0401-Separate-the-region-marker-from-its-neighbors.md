# ZA0401 - Separate the region marker from its neighbors

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
- A marker directly below a `case` or `default` label gets no blank line above it, because the label opens the section.
  In disabled text the label is read line by line, so a label wrapped over lines counts only when its `when` clause starts the last line.
- A comment directly above a marker moves with it, so the blank line goes above the comment.
- A marker at the very start of a file gets no blank line above it, and one at the very end gets none below it, because no unit stands there.
- This rule has no tight counterpart; disable it to keep region markers tight.
- An `#if` and its `#endif` are separated by [ZA0402](https://github.com/Nickelony/Stanza/wiki/ZA0402-Separate-the-conditional-block-from-its-neighbors), not by this rule.
