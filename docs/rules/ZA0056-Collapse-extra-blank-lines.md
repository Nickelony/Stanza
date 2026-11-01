# ZA0056 - Collapse extra blank lines

**Suggestion, on by default.**
Removes the blank lines of a run past [`stanza_max_blank_lines`](https://github.com/Nickelony/Stanza/wiki/stanza_max_blank_lines), wherever the run stands in C# code.

```csharp
// Before
private int _total;



private int _count;

// After
private int _total;

private int _count;
```

- The rule reads every run of blank lines in the trivia between two tokens:
  between members, statements and the units of a list, around comments and directives, and at the start and the end of a file.
- A line that holds only whitespace counts as blank, except a last line that has no line break.
- A comment or a directive ends a run, so the blank lines on each side of it are two runs and each keeps the cap.
- Disabled text, a raw string, a verbatim string and a documentation comment stay as written, because their blank lines are text, not layout.
- The fix removes the first lines of a run and keeps the last ones, so the kept lines stay beside the text below.
  It leaves the line endings of the file as they are.
- The cap never removes the last blank line of a run, so no rule that asks for a blank line can write it back.
- A rule that removes every blank line of a [gap](https://github.com/Nickelony/Stanza/wiki/Terms), such as [`ZA0203`](https://github.com/Nickelony/Stanza/wiki/ZA0203-Remove-brace-padding),
  reports the same lines where it is on, and the fix that applies first leaves nothing for the other.
- [Which rule owns which gap](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap) lists the rules that decide whether a gap holds a blank line at all.
- [`IDE2000`](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/style-rules/ide2000) takes a boolean,
  so it allows any number of blank lines or one. This rule takes the number.
- CSharpier cuts each run to one blank line and has no setting.
  It also drops the blank lines at the end of a file and before a closing brace.
- So this rule agrees with CSharpier at the default cap and disagrees at a cap above `1`;
  [Working with formatters](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters) says how the rules and CSharpier share a file.
