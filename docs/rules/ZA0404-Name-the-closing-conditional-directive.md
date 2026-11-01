# ZA0404 - Name the closing conditional directive

**Suggestion, off by default.**
Writes the condition of an `#if` in a comment after the `#endif` that closes a block of `stanza_min_lines_to_name_endif` lines or more.

```csharp
// Before
#if DEBUG
public void Dump()
{
	Write(_first);
	Write(_second);
	Write(_third);
	Write(_fourth);
	Write(_fifth);
	Write(_sixth);
	Write(_seventh);
}
#endif

// After
#if DEBUG
public void Dump()
{
	Write(_first);
	Write(_second);
	Write(_third);
	Write(_fourth);
	Write(_fifth);
	Write(_sixth);
	Write(_seventh);
}
#endif // DEBUG
```

- The compiler takes only a comment after `#endif`, so the name is a `//` comment, unlike the label of an `#endregion`.
- [`stanza_min_lines_to_name_endif`](https://github.com/Nickelony/Stanza/wiki/stanza_min_lines_to_name_endif) sets the shortest block the rule names, as its `#if` is in view in a short one.
  The lines between the two directives are counted.
- The name is the whole condition of the `#if`, such as `A && !B`, with each run of blanks written as one space.
- Only the `#endif` is named, and it takes the condition of the `#if` whatever `#elif` or `#else` branches stand between.
  Nested blocks pair each `#endif` with the innermost open `#if`.
- The rule writes only on an `#endif` with no comment.
  Any comment already there stays, whether it names the condition or not, so a block with a note is left unnamed rather than losing the note.
- Notes such as `// done`, `// Windows` and `// RELEASE` after `#if DEBUG` all stay.
  A closing `#endregion` that carries a note is left alone in the same way.
- A comment that matches the condition, such as `//DEBUG` or `// A&&B`, is a name already, as blanks are not judged.
  [`ZA0951`](https://github.com/Nickelony/Stanza/wiki/ZA0951-Separate-a-single-line-comment-marker-from-its-text) writes the space after the slashes.
- A directive in disabled text stays as written, so the symbols a [view](https://github.com/Nickelony/Stanza/wiki/Terms) defines decide which blocks it names.
  The name does not depend on the view, so a view never rewrites a name that another view wrote.
- CSharpier leaves the comment after a directive as written, so the rule and the formatter agree.
