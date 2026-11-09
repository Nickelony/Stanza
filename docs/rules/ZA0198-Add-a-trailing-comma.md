# ZA0198 - Add a trailing comma

**Suggestion, off by default.**
Writes a comma after the last element of a list whose closing token stands on a line of its own.

```csharp
// Before
public enum Level
{
	Low,
	High
}

// After
public enum Level
{
	Low,
	High,
}
```

- The rule judges enums, array, collection and object initializers, anonymous objects, `with` initializers,
  collection expressions, switch expressions, property patterns and list patterns.
  The braces of a dictionary entry such as `{ 1, 2 }`, argument lists, attribute lists, tuples and positional patterns are left alone.
- A list on one line, a list whose closing token follows its last element on the same line, and an empty list are left alone.
- The comma goes right after the last element,
  so a comment after the element, a comment above the closing token and a `#region` marker stay where they are.
- A list stays as written where a conditional directive stands between its last element and its closing token,
  because the last element, and so the comma, differ by [view](https://github.com/Nickelony/Stanza/wiki/Terms).
  A directive above the last element does not matter, and the rule writes the same comma in every view.
- [`ZA0199`](https://github.com/Nickelony/Stanza/wiki/ZA0199-Remove-the-trailing-comma) removes the comma instead.
  This rule is the [default half](https://github.com/Nickelony/Stanza/wiki/Terms) of the pair, and it owns the list where a file enables both.
  [The pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) lists the pair.
- CSharpier agrees with this rule, as [Working with formatters](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#layout-that-agrees-with-csharpier) says.
- `SA1413` of StyleCop asks for the same comma.
  It also judges a list whose closing brace follows the last element, which this rule leaves alone,
  and version 1.2.0-beta.556 reports no collection expression.
