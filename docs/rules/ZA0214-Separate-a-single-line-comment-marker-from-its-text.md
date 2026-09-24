# ZA0214 - Separate a single-line comment marker from its text

**Suggestion, off by default.**
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
- The rule treats a punctuation banner like any other text, so `//-----` becomes `// -----`.
  Disable the rule if you draw separators with comment markers.
- The report marks the slashes that need the space.
