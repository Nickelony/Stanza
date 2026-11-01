# ZA0951 - Separate a single-line comment marker from its text

**Suggestion, on by default.**
Puts one space between the marker that opens a single-line comment and the text after it.

```csharp
// Before
//Binds the options from the configuration section of the host.

// After
// Binds the options from the configuration section of the host.
```

- The rule covers `//` and `///` comments, trailing comments and comments on a directive line included,
  so `#endif //DEBUG` becomes `#endif // DEBUG`.
- The rule leaves the text of a `#region`, `#endregion`, `#error` or `#warning` directive alone,
  because that text is the directive's message even where it opens with `//`.
- The rule leaves a marker of four or more slashes alone, because that marker opens commented-out code.
- The rule judges each line of a documentation comment, and a line ends at U+0085, U+2028 and U+2029 as it does at a line feed.
- The rule writes only a missing space; existing whitespace and empty comments stay unchanged.
- The rule leaves a banner alone: a comment whose whole text is one punctuation mark or symbol repeated, such as `//-----`, `//=====` or `//*****`.
  [`stanza_comment_banner_min_run`](https://github.com/Nickelony/Stanza/wiki/stanza_comment_banner_min_run) sets the fewest repeated marks, so a shorter run such as `//--` still becomes `// --`.
- A comment with words, such as `//--- Section ---`, is not a banner.
- The report marks the slashes that need the space.
