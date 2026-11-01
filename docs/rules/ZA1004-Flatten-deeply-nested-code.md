# ZA1004 - Flatten deeply nested code

**Suggestion, off by default.**
Reports a statement that nests deeper than
[`stanza_max_nesting_depth`](https://github.com/Nickelony/Stanza/wiki/stanza_max_nesting_depth) allows.
It is a [report-only rule](https://github.com/Nickelony/Stanza/wiki/Terms), so it has no code fix.

```csharp
// Before - four levels, so a reader holds three conditions to reach the work
foreach (Order order in orders)
{
	if (order.IsOpen)
	{
		foreach (Line line in order.Lines)
		{
			if (line.InStock)
			{
				Ship(line);
			}
		}
	}
}

// After - rewritten by hand, with a guard clause and an extracted method
foreach (Order order in orders)
{
	if (!order.IsOpen)
		continue;

	ShipLines(order);
}
```

- The rule reports once for each body, at the first token of its deepest statement,
  so a method with several deep places gives one diagnostic.
  A body is a method, a constructor, an accessor, a local function, a lambda, an anonymous method or the top-level statements of a file.
- An `if`, `for`, `foreach`, `while`, `do`, `switch`, `try`, `lock` or `using` statement adds a level to the statements inside it.
  An `else if` does not, because it belongs to the chain of the `if` above it, and neither does a bare block or a `using` declaration.
- A lambda, an anonymous method and a local function start a count of their own,
  so the levels around them do not add to the levels inside them.
- The message suggests an early return or an extracted method.
  Either one reshapes the body around the statement, so the rule only reports.
