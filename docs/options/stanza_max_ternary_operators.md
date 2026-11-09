# stanza_max_ternary_operators

Controls how many `?` operators a ternary chain may hold before [`ZA1007`](https://github.com/Nickelony/Stanza/wiki/ZA1007-Break-up-a-long-ternary-chain) reports it.
Default: `2`.
Read by [`ZA1007`](https://github.com/Nickelony/Stanza/wiki/ZA1007-Break-up-a-long-ternary-chain), whose page shows a chain above the default limit.

| Value                                           | Effect                                                     |
| ----------------------------------------------- | ---------------------------------------------------------- |
| `0`                                             | the rule reports no chain                                  |
| a number from 1 to 1000                         | a chain is reported when it holds more operators than that |
| below zero, above 1000, absent, or not a number | the default                                                |

At the default, two operators pass and a third is reported:

```csharp
// Unchanged - two operators
var label = value > 1 ? "high" : value > 0 ? "low" : "none";

// Before - three operators
var label = value > 2 ? "high" : value > 1 ? "medium" : value > 0 ? "low" : "none";

// After
var label = value switch
{
	> 2 => "high",
	> 1 => "medium",
	> 0 => "low",
	_ => "none",
};
```

At a limit of `1`, two operators are reported:

```csharp
// Before at a limit of 1 - two operators
var label = value > 1 ? "high" : value > 0 ? "low" : "none";

// After at a limit of 1
var label = value switch { > 1 => "high", > 0 => "low", _ => "none" };
```

- The count covers the ternaries of one chain: a lambda or an anonymous method starts the count again.
- The operators `??` and `?.` are not ternaries and do not count.
