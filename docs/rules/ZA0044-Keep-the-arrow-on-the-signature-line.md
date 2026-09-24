# ZA0044 - Keep the arrow on the signature line

**Suggestion, off by default.**
Pulls an arrow that opens a line of its own up onto the signature line.

```csharp
// Before
private static Kind Pick(bool first)
	=>
	first ? Kind.One : Kind.Two;

// After
private static Kind Pick(bool first) =>
	first ? Kind.One : Kind.Two;
```

- Only the [gap](https://github.com/Nickelony/Stanza/wiki/Terms) before the arrow changes; the gap after it stays as written.
- A multi-line expression is left alone.
- [`ZA0045`](https://github.com/Nickelony/Stanza/wiki/ZA0045-Put-the-arrow-on-the-line-after-the-signature) places the arrow the other way;
  [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
