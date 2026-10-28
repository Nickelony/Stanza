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

A blank line that another rule asks for stays, and it ends the run:

```csharp
// Before
total = 0;

count = 1;

size = 2;

limit = 3;

Log("a");
Log("b");

// After
total = 0;
count = 1;
size = 2;
limit = 3;

Log("a");
Log("b");
```

Three statements stay as written at the default limit:

```csharp
// Unchanged
total = 0;

count = 1;

size = 2;
```

- The rule reports every blank line of a [loose run](https://github.com/Nickelony/Stanza/wiki/Terms) that holds more statements than the limit allows,
  so four statements, which hold three blank lines, fire it by default.
- The fix of any gap removes the blank lines of its whole run, however many each gap holds, so every fix and Fix All gather the run alike.
- A multi-line statement, a comment, a trailing comment or a directive ends a run, as does a gap with no blank line.
  A directive keeps its gap as [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap) say.
- The rule never removes a blank line that another rule would write back.
  [`ZA0001`](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-declaration-and-invocation-runs) and [`ZA0004`](https://github.com/Nickelony/Stanza/wiki/ZA0004-Separate-same-type-declaration-groups) ask for a blank line around a gathered run of declarations or invocations.
  [`ZA0005`](https://github.com/Nickelony/Stanza/wiki/ZA0005-Separate-multi-line-constructs) asks for one beside a multi-line statement.
- A gap stays blank when gathering it would form a run that one of these rules then sets apart from a tight neighbor.
- A rule that is [off](https://github.com/Nickelony/Stanza/wiki/Terms) at a gap hands it back, so the gap counts as loose again.
- A block or switch section of exactly two statements belongs to [`ZA0006`](https://github.com/Nickelony/Stanza/wiki/ZA0006-Keep-the-two-units-of-a-container-together) where it judges the container.
  They meet only at a limit of `1`, and this rule judges the pair where `ZA0006` is off or skips the container.
- The rule judges the statements of a block and of a switch section, not the gaps between sections.
  A single-line local function counts as a statement, though `ZA0006` skips one.
  Members, top-level statements and the elements of a delimited list keep their blank lines.
