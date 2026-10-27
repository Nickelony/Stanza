# ZA0044 - Keep the arrow on the signature line

**Suggestion, off by default.**
Moves an arrow that opens a line of its own up onto the signature line,
and leaves the expression on the line after it, one indent step deeper.

```csharp
// Before
private static Kind Pick(bool first)
	=> first ? Kind.One : Kind.Two;

// After
private static Kind Pick(bool first) =>
	first ? Kind.One : Kind.Two;
```

- The expression gets a line of its own while [`ZA0043`](https://github.com/Nickelony/Stanza/wiki/ZA0043-Break-the-expression-off-the-signature-line) is enabled, which it is by default;
  where `ZA0043` is off, the expression joins the signature line.
- The rule measures no width.
- A comment beside the arrow keeps it where it is.
- A multi-line expression is left alone.
- [`ZA0045`](https://github.com/Nickelony/Stanza/wiki/ZA0045-Put-the-arrow-on-the-line-after-the-signature) places the arrow the other way;
  [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
