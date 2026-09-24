# ZA0216 - Separate a trailing comment from the code

**Suggestion, off by default.**
Writes the space before a comment that trails code.

```csharp
// Before
ConfigurationSectionOptions options = Bind();// the options of the host

// After
ConfigurationSectionOptions options = Bind(); // the options of the host
```

- The rule writes the space for a `//` comment and a `/* */` comment.
  A documentation comment after code is left alone.
- A `/* */` comment with code after it on its line, as in `Run(/* flag */ true)`, is left alone.
- Only a missing space is written. A comment that already has whitespace before it keeps its column.
- A comment after a preprocessor directive is left alone.
- The report marks the comment that needs the space.
