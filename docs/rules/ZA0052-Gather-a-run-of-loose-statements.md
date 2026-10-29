# ZA0052 - Gather a run of loose statements

**Suggestion, off by default.**
Removes the blank lines between the statements of a [loose run](https://github.com/Nickelony/Stanza/wiki/Terms) that holds more than [`stanza_max_loose_statements`](https://github.com/Nickelony/Stanza/wiki/stanza_max_loose_statements) statements.

```csharp
// Before
total = 0;

count = 1;

size = 2;

limit = 3;

// After
total = 0;
count = 1;
size = 2;
limit = 3;
```

- The rule reports every blank line of a run that holds more statements than the limit allows,
  so four statements, which hold three blank lines, fire it by default.
- The fix of any gap removes the blank lines of its whole run, so every fix and Fix All gather the run alike.
- A multi-line statement, a comment, a trailing comment, a directive or a gap with no blank line ends a run.
  A directive keeps its gap as [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap) say.
- A run never takes in a statement that sits tight against its other neighbor, so the blank line that sets that group apart stays.
  A statement tight against a neighbor that a rule asks to set apart, as `ZA0005` does beside a multi-line statement, still joins the run.
- The rule never removes a blank line that another rule would write back.
- [`ZA0001`](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-declaration-and-invocation-runs) asks for a blank line around a run of declarations or invocations,
  and [`ZA0004`](https://github.com/Nickelony/Stanza/wiki/ZA0004-Separate-same-type-declaration-groups) around a run of declarations of one type.
- [`ZA0005`](https://github.com/Nickelony/Stanza/wiki/ZA0005-Separate-multi-line-constructs) asks for one beside a multi-line statement,
  [`ZA0047`](https://github.com/Nickelony/Stanza/wiki/ZA0047-Separate-the-region-marker-from-its-neighbors) beside a region marker and
  [`ZA0053`](https://github.com/Nickelony/Stanza/wiki/ZA0053-Separate-a-commented-sibling) above a comment.
- A gap stays blank when gathering it would leave `ZA0001` or `ZA0004` asking for a blank line that is not there now.
- A rule that is [off](https://github.com/Nickelony/Stanza/wiki/Terms) at a gap hands it back, so the gap counts as loose again.
- The rule judges the statements of a block and of a switch section, not the gaps between sections.
  Members, top-level statements and the elements of a delimited list keep their blank lines.
- A single-line local function counts as a statement.
