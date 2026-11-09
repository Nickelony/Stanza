# ZA0055 - Keep a run of members together

**Suggestion, off by default.**
Removes the blank lines inside a [kept run](https://github.com/Nickelony/Stanza/wiki/Terms) of members, and writes the blank lines the [grouping rules](https://github.com/Nickelony/Stanza/wiki/Terms) then ask for.

```csharp
// Before
private int first;

private int second;

private int third;
public int Fourth { get; set; }

// After
private int first;
private int second;
private int third;

public int Fourth { get; set; }

// Unchanged - a field and a property
private int first;

public int Second { get; set; }

// Unchanged - two sections of two fields each
private int first;
private int second;

private int third;
private int fourth;

// Unchanged - joining the run would only move the blank line
private int first;

private int second;
public int Third { get; set; }
```

- The rule has no list of kinds. It judges the runs of [`ZA0013`](https://github.com/Nickelony/Stanza/wiki/ZA0013-Separate-member-groups) and [`ZA0014`](https://github.com/Nickelony/Stanza/wiki/ZA0014-Separate-modifier-groups) that are on,
  so a project picks the kinds by enabling them.
- For the fields of a type it judges the runs of [`ZA0002`](https://github.com/Nickelony/Stanza/wiki/ZA0002-Separate-declaration-runs) and [`ZA0003`](https://github.com/Nickelony/Stanza/wiki/ZA0003-Separate-same-type-declaration-runs) that are on too.
- Of two rules that read the same neighbors, the one that reads them apart wins.
  Where `ZA0014` is on, two members with another access, `static` or `readonly` modifier are two runs.
  Where `ZA0003` is on, fields of two types are two runs.
- Where only `ZA0013` is on, all the fields of a type are one run, and so are all its properties.
- The rule removes a blank line only from a gap between two single-line members that holds nothing else.
  A comment, a documentation comment, a trailing comment, a directive or a multi-line member ends the run, so the blank line that
  [`ZA0012`](https://github.com/Nickelony/Stanza/wiki/ZA0012-Separate-a-commented-sibling) or [`ZA0001`](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-multi-line-constructs) asks for stays.
- Joining a run can regroup its members, and the grouping rules then ask for a blank line elsewhere.
  In the first example, the three fields become a run, and `ZA0002` asks for a blank line below it, or `ZA0013` where `ZA0002` is off.
- The fix writes that blank line in the same change, so every fix order and a Fix All write one text.
- The rule reports a run only when joining it removes more blank lines than it writes beyond those other rules already ask for.
  The last example is a [move](https://github.com/Nickelony/Stanza/wiki/Terms) the rule [declines](https://github.com/Nickelony/Stanza/wiki/Terms),
  because joining it would only move the blank line below the second field.
- A blank line between two [sections](https://github.com/Nickelony/Stanza/wiki/Terms) of a run stays as written, as the two-sections example shows.
- The fix writes only the blank lines that stay asked for once the others are written.
  It tries each blank line asked for only once another is written, eight at most, and the grouping rules write the rest.
- The fix of any gap removes the blank lines of its whole run and writes the blank lines that the runs of the container ask for.
- A gap that another rule asks for is not inside a run, such as one that [`ZA0002`](https://github.com/Nickelony/Stanza/wiki/ZA0002-Separate-declaration-runs) or [`ZA0003`](https://github.com/Nickelony/Stanza/wiki/ZA0003-Separate-same-type-declaration-runs) asks for around fields.
- A member of a type, an enum, a namespace or a file is a unit. Imports are no unit, and the top-level statements of a file belong to
  [`ZA0054`](https://github.com/Nickelony/Stanza/wiki/ZA0054-Keep-a-run-of-statements-together).
- A container of two members belongs to [`ZA0051`](https://github.com/Nickelony/Stanza/wiki/ZA0051-Keep-the-two-units-of-a-container-together) where that rule is on.
- The rule leaves a container as written where a conditional directive can change its runs from one [view](https://github.com/Nickelony/Stanza/wiki/Terms) to another,
  on the terms [`ZA0051`](https://github.com/Nickelony/Stanza/wiki/ZA0051-Keep-the-two-units-of-a-container-together) states.
- [`ZA0053`](https://github.com/Nickelony/Stanza/wiki/ZA0053-Gather-a-run-of-loose-members) leaves a container to this rule while this rule has a run in it, so it reads the text that this fix leaves.
  A blank line this rule leaves as a section break or declines as a move stays under `ZA0053` too.
