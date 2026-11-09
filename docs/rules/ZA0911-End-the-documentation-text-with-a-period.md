# ZA0911 - End the documentation text with a period

**Suggestion, off by default.**
Gives the text of an element the period it is missing when its last character is a letter or a digit.

```csharp
// Before
/// <summary>Binds the options from the configuration section</summary>

// After
/// <summary>Binds the options from the configuration section.</summary>
```

- It fires only when the last character of the element's own text is a letter or a digit,
  so text that ends in a question mark, a semicolon, a bracket or a quote is left alone.
- The fix appends one period and nothing else.
- Text that ends on a link or a path, a word holding `://`, `/` or `\`, is left alone, because a period would join the address.
- Content that ends on a nested element is left alone, because its last character belongs to that element.
  A `<see langword>` element is the exception: it stands for the last word, so the period follows it.
- Text that stands outside every element, such as a comment with no tags, is left alone.
- An inline element, a `<code>` block, a `<list>` and a `<table>` are never judged.
- A top-level element that holds a `<code>` block, an `<include>` or an `<inheritdoc>` is never judged, and the elements beside it are.
- Each `<para>` in a `<remarks>` block is judged on its own, so each paragraph needs its own period.
- [Limitations](https://github.com/Nickelony/Stanza/wiki/Limitations) lists the documentation comments this rule leaves alone.
- [`ZA0910`](https://github.com/Nickelony/Stanza/wiki/ZA0910-Capitalize-the-documentation-text) capitalizes the start of the same text.
- The report marks the last character before the missing period.
