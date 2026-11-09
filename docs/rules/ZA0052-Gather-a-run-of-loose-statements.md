# ZA0052 - Gather a run of loose statements

**Suggestion, off by default.**
Removes the blank lines between the statements of a [loose run](https://github.com/Nickelony/Stanza/wiki/Terms) that holds more than [`stanza_max_loose_run_size`](https://github.com/Nickelony/Stanza/wiki/stanza_max_loose_run_size) statements.

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
  so a run past the [default limit](https://github.com/Nickelony/Stanza/wiki/stanza_max_loose_run_size) fires it.
- The fix of any gap removes the blank lines of its whole run, so every fix and Fix All gather the run alike.
- A multi-line statement, a comment, a trailing comment, a directive or a gap with no blank line ends a run.
  A directive keeps its gap as [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap) say.
- A container where a conditional directive can change the runs from one [view](https://github.com/Nickelony/Stanza/wiki/Terms) to another is left as written,
  on the terms [`ZA0051`](https://github.com/Nickelony/Stanza/wiki/ZA0051-Keep-the-two-units-of-a-container-together) states.
- A run never takes in a statement that sits tight against its other neighbor, so the blank line that sets that group apart stays.
  A statement tight against a neighbor that a rule asks to set apart, as `ZA0001` does beside a multi-line statement, still joins the run.
- The rule never removes a blank line that another rule would write back.
- The [grouping rules](https://github.com/Nickelony/Stanza/wiki/Terms) ask for a blank line around a run of statements of one kind.
- [`ZA0001`](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-multi-line-constructs) asks for one beside a multi-line statement,
  [`ZA0401`](https://github.com/Nickelony/Stanza/wiki/ZA0401-Separate-the-region-marker-from-its-neighbors) beside a region marker,
  [`ZA0012`](https://github.com/Nickelony/Stanza/wiki/ZA0012-Separate-a-commented-sibling) above a comment and
  [`ZA0010`](https://github.com/Nickelony/Stanza/wiki/ZA0010-Separate-the-base-call) beside the base call that opens or closes a member body.
- [`ZA0009`](https://github.com/Nickelony/Stanza/wiki/ZA0009-Separate-a-jump-statement) asks for a blank line above a jump when the stanza above it spans two lines,
  and this rule judges that gap as gathered.
- [`ZA0011`](https://github.com/Nickelony/Stanza/wiki/ZA0011-Separate-the-guard-clauses) asks for one below the guards that open a block, and this rule keeps that gap blank.
- So the gap above a jump stays blank when the run above it is gathered, and a run that then holds no more than the limit is left as written.
- A gap stays blank when gathering it would leave a grouping rule, `ZA0009` or `ZA0011`
  asking for a blank line that is not there now.
- A rule that is [off](https://github.com/Nickelony/Stanza/wiki/Terms) at a gap hands it back, so the gap counts as loose again.
- The rule judges the statements of a block, of a switch section and at the top level of a file, not the gaps between switch sections.
  Imports are no statements, and [`ZA0015`](https://github.com/Nickelony/Stanza/wiki/ZA0015-Separate-the-import-section) asks for the blank line below them.
- Members and the elements of a delimited list keep their blank lines.
  [`ZA0053`](https://github.com/Nickelony/Stanza/wiki/ZA0053-Gather-a-run-of-loose-members) gathers a run of members, such as the types below the top-level statements of a file.
- A single-line local function counts as a statement.
- Where [`ZA0054`](https://github.com/Nickelony/Stanza/wiki/ZA0054-Keep-a-run-of-statements-together) is on and has a run in the container, this rule leaves the container to it.
  That fix joins its run first and writes the blank lines the runs of the container ask for, so this rule then reads the text it leaves.
- A blank line that `ZA0054` leaves as a [section break](https://github.com/Nickelony/Stanza/wiki/Terms) or [declines](https://github.com/Nickelony/Stanza/wiki/Terms) as a move stays as written,
  since it sits beside a tight statement or comes back.
