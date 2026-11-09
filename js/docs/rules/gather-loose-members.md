# gather-loose-members

**Level: strong.**
Removes the blank lines between the members of a loose run that holds more than `maxLooseRunSize` members,
so a class does not read as a column of lines that each stand alone.

```ts
// Before
class Order {
	total = 0;

	count = 1;

	size = 2;

	limit = 3;
}

// After
class Order {
	total = 0;
	count = 1;
	size = 2;
	limit = 3;
}
```

- A member is a single-line unit of a class body, an interface body or a type literal.
- A loose run, what ends it, and how a member that sits tight or that another rule asks to set apart counts are those of [`gather-loose-statements`](gather-loose-statements.md).
- The rule never removes a blank line that another enabled rule asks for, so a run that [`separate-member-groups`](separate-member-groups.md)
  or [`separate-modifier-groups`](separate-modifier-groups.md) ends is counted up to that gap.
- The limit is the `maxLooseRunSize` [setting](gather-loose-statements.md#settings), which both gather rules read.
- A container of two members belongs to [`keep-two-units-together`](keep-two-units-together.md).
- The statements of a block belong to [`gather-loose-statements`](gather-loose-statements.md).
- Where [`keep-member-runs-together`](keep-member-runs-together.md) is also enabled, it reports a blank line that both rules remove.

## C# counterpart

[ZA0053 - Gather a run of loose members](https://github.com/Nickelony/Stanza/wiki/ZA0053-Gather-a-run-of-loose-members),
whose limit is [`stanza_max_loose_run_size`](https://github.com/Nickelony/Stanza/wiki/stanza_max_loose_run_size).
