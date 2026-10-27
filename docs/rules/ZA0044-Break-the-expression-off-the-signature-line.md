# ZA0044 - Break the expression off the signature line

**Suggestion, on by default.**
Breaks an expression body onto a second line when the signature and the expression do not fit on one line within [`stanza_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_max_line_length).

The examples set [`stanza_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_max_line_length) to `60`.
At the default width, the member fits and nothing is reported.

```csharp
// Before
private static Kind Pick(bool first) => first ? Kind.One : Kind.Two;

// After - the trailing arrow stays where it was
private static Kind Pick(bool first) =>
	first ? Kind.One : Kind.Two;

// After - with ZA0046 enabled, the arrow opens its own line
private static Kind Pick(bool first)
	=> first ? Kind.One : Kind.Two;
```

- The arrow stays where it is,
  unless [`ZA0046`](https://github.com/Nickelony/Stanza/wiki/ZA0046-Put-the-arrow-on-the-line-after-the-signature) owns the arrow's gap at that member,
  as [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) says; then the arrow opens its own line.
- [`stanza_multiline_body_trigger`](https://github.com/Nickelony/Stanza/wiki/stanza_multiline_body_trigger) decides whether the arrow counts toward the line's width.
- A member whose signature or expression already spans lines is left alone.
- A comment between the signature and the arrow counts toward the width of the arrow's line.
- Nothing is reported when even the broken form is wider than the limit,
  because the break would not make the lines fit.
- A `stanza_max_line_length` narrower than the formatter's print width lets CSharpier rejoin the lines this rule breaks;
  see [Working with formatters](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters).
