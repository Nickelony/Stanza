# ZA0053 - Gather a run of loose members

**Suggestion, off by default.**
Removes the blank lines between the members of a [loose run](https://github.com/Nickelony/Stanza/wiki/Terms) that holds more than [`stanza_max_loose_run_size`](https://github.com/Nickelony/Stanza/wiki/stanza_max_loose_run_size) members.

```csharp
// Before
private int total;

private int count;

private int size;

private int limit;

// After
private int total;
private int count;
private int size;
private int limit;
```

- The rule reports every blank line of a run that holds more members than the limit allows,
  so a run past the [default limit](https://github.com/Nickelony/Stanza/wiki/stanza_max_loose_run_size) fires it.
- The fix of any gap removes the blank lines of its whole run, so every fix and Fix All gather the run alike.
- A member of a type, an enum, a namespace or a file is a unit.
  Imports are no unit, and the top-level statements of a file belong to [`ZA0052`](https://github.com/Nickelony/Stanza/wiki/ZA0052-Gather-a-run-of-loose-statements).
- A multi-line member, a comment, a documentation comment, a trailing comment, a directive or a gap with no blank line ends a run.
  An attribute on its own line makes its member multi-line.
  A directive keeps its gap as [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap) say.
- The gap of an enum member starts below its comma, so a comment that trails the comma ends a run.
- A container where a conditional directive can change the runs from one [view](https://github.com/Nickelony/Stanza/wiki/Terms) to another is left as written,
  on the terms [`ZA0051`](https://github.com/Nickelony/Stanza/wiki/ZA0051-Keep-the-two-units-of-a-container-together) states.
- A run never takes in a member that sits tight against its other neighbor, so the blank line that sets that group apart stays.
  A member tight against a neighbor that a rule asks to set apart, as `ZA0001` does beside a multi-line member, still joins the run.
- The rule never removes a blank line that another rule would write back.
- The [grouping rules](https://github.com/Nickelony/Stanza/wiki/Terms) ask for a blank line around a run of declarations of one type, of members of one kind
  and of members that share their access, `static` and `readonly` modifiers.
- [`ZA0001`](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-multi-line-constructs) asks for one beside a multi-line member,
  [`ZA0015`](https://github.com/Nickelony/Stanza/wiki/ZA0015-Separate-the-import-section) below the imports,
  [`ZA0401`](https://github.com/Nickelony/Stanza/wiki/ZA0401-Separate-the-region-marker-from-its-neighbors) beside a region marker and
  [`ZA0012`](https://github.com/Nickelony/Stanza/wiki/ZA0012-Separate-a-commented-sibling) above a comment.
- A gap stays blank when gathering it would leave a grouping rule asking for a blank line that is not there now.
- A rule that is [off](https://github.com/Nickelony/Stanza/wiki/Terms) at a gap hands it back, so the gap counts as loose again.
- A container of two members belongs to [`ZA0051`](https://github.com/Nickelony/Stanza/wiki/ZA0051-Keep-the-two-units-of-a-container-together) where that rule is on.
- The gap below a file-scoped namespace header belongs to [`ZA0017`](https://github.com/Nickelony/Stanza/wiki/ZA0017-Separate-a-file-scoped-namespace-declaration-from-its-first-member),
  and the elements of a delimited list keep their blank lines.
- Where [`ZA0055`](https://github.com/Nickelony/Stanza/wiki/ZA0055-Keep-a-run-of-members-together) is on and has a run in the container, this rule leaves the container to it.
  That fix joins its run first and writes the blank lines the runs of the container ask for, so this rule then reads the text it leaves.
- A blank line that `ZA0055` leaves as a [section break](https://github.com/Nickelony/Stanza/wiki/Terms) or [declines](https://github.com/Nickelony/Stanza/wiki/Terms) as a move stays as written,
  since it sits beside a tight member or comes back.
