# ZA0010 - Separate the base call

**Suggestion, off by default.**
Adds a blank line below a call to the base implementation that opens a member body, and above one that closes it,
so the base call stands apart from the member's own work.

```csharp
// Before
protected override void OnStart()
{
	base.OnStart();
	_timer.Start();
	Log("Started.");
}

// After
protected override void OnStart()
{
	base.OnStart();

	_timer.Start();
	Log("Started.");
}
```

```csharp
// Before
protected override void Dispose(bool disposing)
{
	_timer.Dispose();
	_stream.Dispose();
	base.Dispose(disposing);
}

// After
protected override void Dispose(bool disposing)
{
	_timer.Dispose();
	_stream.Dispose();

	base.Dispose(disposing);
}
```

A returned base call is a jump, which this rule leaves to [`ZA0009`](https://github.com/Nickelony/Stanza/wiki/ZA0009-Separate-a-jump-statement):

```csharp
// Unchanged
public override string ToString()
{
	Validate();
	Refresh();
	return base.ToString();
}
```

- A base call is a statement that calls a member through a chain that starts at `base`,
  such as `base.Load();`, `await base.LoadAsync();`, `_ = base.Load();` or `base.Items.Clear();`.
- An assignment such as `base.Value = value;` is no call, and neither is a call that passes `base.Value` as an argument.
- The rule judges the block body of a method, a constructor, a finalizer and an accessor.
  A local function, a lambda and a nested block such as the body of an `if` are left alone,
  because a base call there is not the call the member extends.
- The opening call is the first statement below the guard clauses that open the body, as
  [`ZA0011`](https://github.com/Nickelony/Stanza/wiki/ZA0011-Separate-the-guard-clauses) reads them,
  so `ZA0011` owns the blank line above the call and this rule the one below it.
- The closing call is the last statement of the body. A body that opens and closes with a base call gets both blank lines,
  or one between two adjacent calls,
  and a base call alone in its body has no gap to set apart.
- `return base.Load();` is a jump, so `ZA0009` owns the gap above it and asks for it only when the
  [stanza](https://github.com/Nickelony/Stanza/wiki/Terms) above spans two or more lines.
- This rule owns the gap below an opening call even when a jump follows it,
  and the gap above a closing call that stands directly below the guards, so `ZA0009` and `ZA0011` leave those gaps.
- The gap beside a base call ends a run for the
  [grouping rules](https://github.com/Nickelony/Stanza/wiki/Terms),
  so a call of the same name below `base.Clear();` starts a run of its own, and the grouping rules leave the gap to this rule.
- The gap also ends the stanza that `ZA0009` reads above a jump,
  and [`ZA0052`](https://github.com/Nickelony/Stanza/wiki/ZA0052-Gather-a-run-of-loose-statements) keeps it blank when it gathers a run.
- [`ZA0001`](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-multi-line-constructs) owns the gap beside a multi-line statement,
  [`ZA0012`](https://github.com/Nickelony/Stanza/wiki/ZA0012-Separate-a-commented-sibling) the gap above a comment,
  and [`ZA0051`](https://github.com/Nickelony/Stanza/wiki/ZA0051-Keep-the-two-units-of-a-container-together) a body of two statements.
  A rule that is [off](https://github.com/Nickelony/Stanza/wiki/Terms) at a gap hands it back, so this rule writes the blank line.
- A comment directly above the statement below the call moves with it, so the blank line goes above the comment.
- A directive in the gap keeps it as written.
- A pragma that silences this rule at a gap hands the gap back, and the grouping rules read a run across it again.
- This rule has no tight counterpart; leave it disabled to keep base calls tight.
