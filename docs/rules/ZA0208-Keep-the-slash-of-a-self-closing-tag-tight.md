# ZA0208 - Keep the slash of a self-closing tag tight

**Suggestion, off by default.**
Removes the space before the slash of a self-closing tag.

```csharp
// Before
/// <returns><see langword="null" /> when the section is missing.</returns>

// After
/// <returns><see langword="null"/> when the section is missing.</returns>
```

- [`ZA0207`](https://github.com/Nickelony/Stanza/wiki/ZA0207-Separate-the-slash-of-a-self-closing-tag) writes the spaced form instead and states which form another rule's tag takes;
  [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
- A slash [gap](https://github.com/Nickelony/Stanza/wiki/Terms) that spans a line is left to [`ZA0201`](https://github.com/Nickelony/Stanza/wiki/ZA0201-Lay-out-a-documentation-comment).
- Markup that [`ZA0207`](https://github.com/Nickelony/Stanza/wiki/ZA0207-Separate-the-slash-of-a-self-closing-tag) leaves as written stays as written here.
