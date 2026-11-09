# ZA0199 - Remove the trailing comma

**Suggestion, off by default.**
Removes the comma after the last element of a list whose closing token stands on a line of its own.

```csharp
// Before
public enum Level
{
	Low,
	High,
}

// After
public enum Level
{
	Low,
	High
}
```

- The rule judges the same lists as [`ZA0198`](https://github.com/Nickelony/Stanza/wiki/ZA0198-Add-a-trailing-comma), which writes the comma instead.
  [The pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms), and a file that enables both gets `ZA0198`.
- A list on one line, a list whose closing token follows its last element on the same line, and a list that ends without a comma are left alone.
  So is a comma on a line of its own.
- The blanks before the comma go with it, unless a comment follows the comma with no blank between them.
  A comment after the comma, a comment between the element and the comma and a comment above the closing token stay.
- A list stays as written where a conditional directive stands between its last element and its closing token,
  because the last element, and so the comma, differ by [view](https://github.com/Nickelony/Stanza/wiki/Terms).
- CSharpier writes back the comma this rule removes, so the rule and the formatter undo each other,
  as [Working with formatters](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#layout-that-csharpier-undoes) says.
- `SA1413` of StyleCop conflicts with this rule, as [`ZA0198`](https://github.com/Nickelony/Stanza/wiki/ZA0198-Add-a-trailing-comma) says, so a project enables one of the two.
