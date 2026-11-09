# keep-member-runs-together

**Level: opt-in.**
Removes the blank lines inside a run of single-line members that the enabled member rules read as one.

```ts
// Before
class Widget {
	count = 0;

	name = '';

	reset() {}

	clear() {}
}

// After
class Widget {
	count = 0;
	name = '';

	reset() {}
	clear() {}
}

// Unchanged - two sections of two fields each
class Widget {
	count = 0;
	name = '';

	size = 1;
	limit = 2;
}
```

The example runs with [`separate-member-groups`](separate-member-groups.md) enabled, which reads the fields as one run and the methods as another.

- The rule has no list of kinds. It reads the runs of [`separate-member-groups`](separate-member-groups.md)
  and [`separate-modifier-groups`](separate-modifier-groups.md) that are enabled, so a project picks the kinds by enabling them.
- Of two rules that read the same neighbors, the one that reads them apart wins.
  Where the modifier rule is enabled, two members with other modifiers are two runs and the blank line between them stays.
- The rule judges nothing while both of those rules are off.
- The rule removes a blank line only between two single-line members that hold nothing else.
  A comment, a trailing comment or a multi-line member ends the run.
- A blank line between two sections stays as written, a section being at least two members of one run that stand tight against each other.
  A lone member beside a section is absorbed, and so are two lone members.
- A lone member between two sections is a section of one, so both its blank lines stay.
- Two members on one line stand tight against each other, so two such lines with a blank line between are two sections.
- It never removes a blank line another enabled rule asks for, so the blank lines around a run stay or are written.
- The fix of any gap removes the blank lines of its whole run, so every fix order joins the run alike.
- The rule judges the members of a class, an interface and a type literal.
  The statements of a block belong to [`keep-statement-runs-together`](keep-statement-runs-together.md).
- A container of two members belongs to [`keep-two-units-together`](keep-two-units-together.md).
- The rule is in no preset, because it removes blank lines an author wrote on purpose; enable it with an entry of its own.

## C# counterpart

[ZA0055 - Keep a run of members together](https://github.com/Nickelony/Stanza/wiki/ZA0055-Keep-a-run-of-members-together).
