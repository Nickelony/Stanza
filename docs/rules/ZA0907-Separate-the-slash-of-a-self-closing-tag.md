# ZA0907 - Separate the slash of a self-closing tag

**Suggestion, off by default.**
Puts exactly one space before the slash of a tag that closes itself.

```csharp
// Before
/// <returns><see langword="null"/> when the section is missing.</returns>

// After
/// <returns><see langword="null" /> when the section is missing.</returns>
```

- [`ZA0908`](https://github.com/Nickelony/Stanza/wiki/ZA0908-Keep-the-slash-of-a-self-closing-tag-tight) writes the tight form instead;
  [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
- The tag that another rule writes, such as the empty element of [`ZA0909`](https://github.com/Nickelony/Stanza/wiki/ZA0909-Write-an-empty-element-as-a-self-closing-tag) and the keywords of [`ZA0912`](https://github.com/Nickelony/Stanza/wiki/ZA0912-Mark-the-language-keywords),
  follows the owner of the slash gap at that comment.
  It takes the spaced form where this rule owns the gap, as [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) says, and the tight form elsewhere.
- The [gap](https://github.com/Nickelony/Stanza/wiki/Terms) is judged only when it stays on one line.
  A tag whose attributes wrap onto its slash is left to [`ZA0901`](https://github.com/Nickelony/Stanza/wiki/ZA0901-Lay-out-a-documentation-comment).
- A slash inside a `<list>`, a `<table>`, a `<code>` sample or content marked `xml:space="preserve"` is left as written.
