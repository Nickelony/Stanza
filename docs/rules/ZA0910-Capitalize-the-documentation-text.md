# ZA0910 - Capitalize the documentation text

**Suggestion, off by default.**
Capitalizes the text of an element when its first character is a lowercase letter.

```csharp
// Before
/// <summary>binds the options from the configuration section.</summary>

// After
/// <summary>Binds the options from the configuration section.</summary>
```

- The fix writes the uppercase form of that one letter, in the invariant culture, and changes nothing else.
- A letter with no single uppercase form, such as `ß`, is left alone.
- Text whose first character is not a letter, such as a digit, a quote or a bracket, is left alone.
- Content that starts with a nested element is left alone, because its first word belongs to that element.
  That covers `<see cref>`, `<paramref>`, `<typeparamref>`, `<c>` and `<see langword>`.
- A first word with an uppercase letter after its first character keeps its case, as in `iOS`, `macOS`, `eBay` and `maxCount`.
- A first word that is a link, a path or a file name is left alone: a word holding `://`, `/`, `\`, or a dot inside it as in `appsettings.json`.
- A first word that [`stanza_doc_langwords`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_langwords) lists, `null` by default, is left alone,
  so [`ZA0912`](https://github.com/Nickelony/Stanza/wiki/ZA0912-Mark-the-language-keywords) still marks it, whichever rule fixes first.
- A brand written in lowercase, such as `npm`, is capitalized, because the rule cannot tell it from an ordinary word.
- Text that stands outside every element, such as a comment with no tags, is left alone.
- An inline element, a `<code>` block, a `<list>` and a `<table>` are never judged.
- A top-level element that holds a `<code>` block, an `<include>` or an `<inheritdoc>` is never judged, and the elements beside it are.
- Each `<para>` in a `<remarks>` block is judged on its own, so each paragraph needs its own capital.
- [`ZA0911`](https://github.com/Nickelony/Stanza/wiki/ZA0911-End-the-documentation-text-with-a-period) closes the same text with a period.
- [Limitations](https://github.com/Nickelony/Stanza/wiki/Limitations) lists the documentation comments this rule leaves alone.
- The report marks the letter the fix capitalizes.

```csharp
// Unchanged
/// <summary><see langword="true"/> if the section exists.</summary>
/// <returns>iOS reads appsettings.json first.</returns>
```
