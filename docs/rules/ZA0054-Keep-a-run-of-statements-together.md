# ZA0054 - Keep a run of statements together

**Suggestion, off by default.**
Removes the blank lines inside a [kept run](https://github.com/Nickelony/Stanza/wiki/Terms) of statements, and writes the blank lines the [grouping rules](https://github.com/Nickelony/Stanza/wiki/Terms) then ask for.

```csharp
// Before
int width = 0;

int height = 0;

int depth = 0;
string name = "";

// After
int width = 0;
int height = 0;
int depth = 0;

string name = "";

// Unchanged - two declarations of different types
int width = 0;

string name = "";

// Unchanged - two sections of two statements each
int width = 0;
int height = 0;

int top = 0;
int left = 0;

// Unchanged - joining the run would only move the blank line
int width = 0;

int height = 0;
string name = "";
```

- The rule has no list of kinds. It judges the runs of the grouping rules that are on, so a project picks the kinds by enabling them.
- A statement is in a run only where the rule of its kind is on, except that
  [`ZA0003`](https://github.com/Nickelony/Stanza/wiki/ZA0003-Separate-same-type-declaration-runs) makes runs of declarations of one type by itself.
- Of two rules that read the same neighbors, the one that reads them apart wins.
  Where `ZA0003` is on, declarations of two types are two runs, and the blank line between them stays.
  Where it is off, they are one run of declarations while `ZA0002` is on.
- Where [`stanza_assignment_targets`](https://github.com/Nickelony/Stanza/wiki/stanza_assignment_targets) is `split`, the assignments it sets apart are two runs.
- The rule removes a blank line only from a gap between two single-line statements that holds nothing else.
  A comment, a trailing comment, a directive or a multi-line statement ends the run, so the blank line that
  [`ZA0012`](https://github.com/Nickelony/Stanza/wiki/ZA0012-Separate-a-commented-sibling) or [`ZA0001`](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-multi-line-constructs) asks for stays.
- Joining a run can regroup its statements, and the grouping rules then ask for a blank line elsewhere.
  In the first example, which has `ZA0003` on, the three declarations become a run of one type, and `ZA0003` asks for a blank line below it.
- The fix writes that blank line in the same change, so every fix order and a Fix All write one text.
- The rule reports a run only when joining it removes more blank lines than it writes beyond those other rules already ask for.
  The last example is a [move](https://github.com/Nickelony/Stanza/wiki/Terms) the rule [declines](https://github.com/Nickelony/Stanza/wiki/Terms),
  because joining it would only move the blank line below the height.
- A blank line between two [sections](https://github.com/Nickelony/Stanza/wiki/Terms) of a run stays as written, as the two-sections example shows.
- The fix writes only the blank lines that stay asked for once the others are written.
  It tries each blank line asked for only once another is written, eight at most, and the grouping rules write the rest.
- The fix of any gap removes the blank lines of its whole run and writes the blank lines that the runs of the container ask for.
- A gap that another rule asks for is not inside a run, such as the one below the base call that [`ZA0010`](https://github.com/Nickelony/Stanza/wiki/ZA0010-Separate-the-base-call) asks for.
- A container of two statements belongs to [`ZA0051`](https://github.com/Nickelony/Stanza/wiki/ZA0051-Keep-the-two-units-of-a-container-together) where that rule is on.
- The rule leaves a container as written where a conditional directive can change its runs from one [view](https://github.com/Nickelony/Stanza/wiki/Terms) to another,
  on the terms [`ZA0051`](https://github.com/Nickelony/Stanza/wiki/ZA0051-Keep-the-two-units-of-a-container-together) states.
- [`ZA0052`](https://github.com/Nickelony/Stanza/wiki/ZA0052-Gather-a-run-of-loose-statements) leaves a container to this rule while this rule has a run in it, so it reads the text that this fix leaves.
  A blank line this rule leaves as a section break or declines as a move stays under `ZA0052` too.
- Members and the types of a file belong to [`ZA0055`](https://github.com/Nickelony/Stanza/wiki/ZA0055-Keep-a-run-of-members-together).
