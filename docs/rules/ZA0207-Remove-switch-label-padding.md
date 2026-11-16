# ZA0207 - Remove switch label padding

**Suggestion, on by default.**
Removes the blank line below a `case` or `default` label, before the next label or the first statement of its section.

```csharp
// Before
switch (kind)
{
	case Kind.Draft:

	case Kind.Pending:

		Review(order);
		break;
}

// After
switch (kind)
{
	case Kind.Draft:
	case Kind.Pending:
		Review(order);
		break;
}
```

- Only whitespace is removed, and a directive in the [gap](https://github.com/Nickelony/Stanza/wiki/Terms) leaves it alone.
- With a comment in the gap, the blank lines before the first comment go, and the ones between the comment and the statement stay.
- The blank line between two sections is no padding of a label; [`ZA0111`](https://github.com/Nickelony/Stanza/wiki/ZA0111-Separate-multi-line-switch-sections) and [`ZA0113`](https://github.com/Nickelony/Stanza/wiki/ZA0113-Keep-single-line-switch-sections-together) own it.
- A block that opens a section leaves its gap to [`ZA0203`](https://github.com/Nickelony/Stanza/wiki/ZA0203-Remove-brace-padding) where that rule is on, because the gap lies between a header and its `{`.
  Where `ZA0203` is [off](https://github.com/Nickelony/Stanza/wiki/Terms), this rule removes it.
- A label of a `goto` is no switch label.
- CSharpier keeps a blank line below a label as written, so it leaves this rule's removal in place, as [Working with formatters](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#layout-that-agrees-with-csharpier) says.
