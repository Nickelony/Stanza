# stanza_max_nesting_depth

Controls how many levels of control flow a statement may sit in before [`ZA1004`](https://github.com/Nickelony/Stanza/wiki/ZA1004-Flatten-deeply-nested-code) reports it.
Default: `3`.
Read by [`ZA1004`](https://github.com/Nickelony/Stanza/wiki/ZA1004-Flatten-deeply-nested-code), whose page shows a nest above the default limit.

| Value                                           | Effect                                            |
| ----------------------------------------------- | ------------------------------------------------- |
| `0`                                             | the rule reports no body                          |
| a number from 1 to 1000                         | a body is reported when it nests deeper than that |
| below zero, above 1000, absent, or not a number | the default                                       |

At the default, three levels pass and a fourth is reported:

```csharp
// Unchanged - three levels
foreach (Order order in orders)
{
	if (order.IsOpen)
	{
		foreach (Line line in order.Lines)
		{
			Ship(line);
		}
	}
}

// Before - four levels
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

// After
foreach (Order order in orders)
{
	if (!order.IsOpen)
		continue;

	ShipLines(order);
}
```

At a limit of `1`, two levels are reported:

```csharp
// Before at a limit of 1 - two levels
if (order.IsOpen)
{
	if (order.HasLines)
	{
		Ship(order);
	}
}

// After at a limit of 1
if (order.IsOpen && order.HasLines)
{
	Ship(order);
}
```

- The count covers one body: a lambda, an anonymous method or a local function starts it again.
- An `else if` and a bare block add no level.
