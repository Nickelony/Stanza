# ZA0952 - Separate a delimited comment marker from its text

**Suggestion, on by default.**
Separates comment text from either marker when the text touches it.

```csharp
// Before
/*Binds the options from the configuration section.*/

// After
/* Binds the options from the configuration section. */
```

```csharp
// Unchanged - a run of stars touches each marker
/***** Options *****/
```

- It is the delimited-comment form of [`ZA0951`](https://github.com/Nickelony/Stanza/wiki/ZA0951-Separate-a-single-line-comment-marker-from-its-text),
  which separates the marker of a single-line comment from its text.
- Only the missing space is written, and a comment that holds nothing but stars and whitespace, such as `/***/`, is left alone.
- A `/** */` documentation comment is left alone.
- The rule leaves a marker alone when a banner run touches it: one punctuation mark or symbol repeated up to a blank or the other marker,
  as in `/*--------*/` and at both ends of a box comment.
  [`stanza_comment_banner_min_run`](https://github.com/Nickelony/Stanza/wiki/stanza_comment_banner_min_run) sets the fewest repeated marks.
- The run counts the marks after `/*` and before `*/`, so `/*** Options ***/` holds runs of two and becomes `/* ** Options ** */`.
- A run that touches a word, such as `/*--Options--*/`, is not a banner.
- The lines between the markers of a delimited comment are the author's,
  so the rule judges only the two ends, and each on its own.
- The report marks the `/*` or the `*/` that needs the space.
