# keep-statement-runs-together

**Level: opt-in.**
Removes the blank lines inside a run of single-line statements that the enabled run rules read as one.

```ts
// Before
log('first');

log('second');

log('third');

// After
log('first');
log('second');
log('third');

// Unchanged - two sections of two statements each
log('first');
log('second');

log('third');
log('fourth');
```

- The rule has no list of kinds. It reads the runs of [`separate-declaration-runs`](separate-declaration-runs.md),
  [`separate-invocation-runs`](separate-invocation-runs.md), [`separate-assertion-runs`](separate-assertion-runs.md),
  [`separate-await-runs`](separate-await-runs.md) and [`separate-assignment-runs`](separate-assignment-runs.md) that are enabled,
  so a project picks the kinds by enabling them.
- The rule removes a blank line only between two single-line statements that hold nothing else.
  A comment, a trailing comment, an import, a directive or a multi-line statement ends the run.
- A blank line between two sections stays as written, a section being at least two statements of one run that stand tight against each other.
  A lone statement beside a section is absorbed, and so are two lone statements.
- A lone statement between two sections is a section of one, so both its blank lines stay.
- Two statements on one line stand tight against each other, so two such lines with a blank line between are two sections.
- It never removes a blank line another enabled rule asks for, so the blank lines around a run stay or are written.
- The fix of any gap removes the blank lines of its whole run, so every fix order joins the run alike.
- The rule judges the statements of a block, a switch case, a static block, a module block and a file.
  The members of a class, an interface and a type literal belong to [`keep-member-runs-together`](keep-member-runs-together.md).
- A container of two statements belongs to [`keep-two-units-together`](keep-two-units-together.md), and the elements of a list keep their blank lines.
- The rule is in no preset, because it removes blank lines an author wrote on purpose; enable it with an entry of its own.

## C# counterpart

[ZA0054 - Keep a run of statements together](https://github.com/Nickelony/Stanza/wiki/ZA0054-Keep-a-run-of-statements-together).
