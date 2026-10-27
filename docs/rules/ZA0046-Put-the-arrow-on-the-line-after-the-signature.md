# ZA0046 - Put the arrow on the line after the signature

**Suggestion, off by default.**
Moves the arrow from the end of the signature line onto a line of its own,
one indent step deeper, and puts the expression after the arrow.

```csharp
// Before
private static Kind Pick(bool first) =>
	first ? Kind.One : Kind.Two;

// After
private static Kind Pick(bool first)
	=> first ? Kind.One : Kind.Two;
```

- [`ZA0045`](https://github.com/Nickelony/Stanza/wiki/ZA0045-Keep-the-arrow-on-the-signature-line) places the arrow the other way;
  [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
- The rule accepts an arrow on its own line at the member's column or deeper, with the expression after it;
  the fix moves it one indent step deeper than the member.
- The rule measures no width, so it moves the arrow whatever the line length.
- A member whose signature, arrow and expression share one line is left alone.
- A multi-line expression is left alone.
