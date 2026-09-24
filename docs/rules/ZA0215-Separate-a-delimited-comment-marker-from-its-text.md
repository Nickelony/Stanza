# ZA0215 - Separate a delimited comment marker from its text

**Suggestion, off by default.**
Separates comment text from either marker when the text touches it.

```csharp
// Before
/*Binds the options from the configuration section.*/

// After
/* Binds the options from the configuration section. */
```

- It is the delimited-comment form of [`ZA0214`](https://github.com/Nickelony/Stanza/wiki/ZA0214-Separate-a-single-line-comment-marker-from-its-text),
  which separates the marker of a single-line comment from its text.
- Only the missing space is written, and a comment that holds nothing but stars and whitespace, such as `/***/`, is left alone.
- A `/** */` documentation comment is left alone.
- The rule treats a punctuation banner like any other text, so `/***** Options *****/` becomes `/* **** Options **** */`.
  Disable the rule if you draw banners with stars.
- The lines between the markers of a delimited comment are the author's,
  so the rule judges only the two ends, and each on its own.
- The report marks the `/*` or the `*/` that needs the space.
