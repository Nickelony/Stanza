# stanza_max_logical_operators

Controls how many `&&` and `||` operators a condition may hold before [`ZA1005`](https://github.com/Nickelony/Stanza/wiki/ZA1005-Shorten-a-long-condition) reports it.
Default: `4`.
Read by [`ZA1005`](https://github.com/Nickelony/Stanza/wiki/ZA1005-Shorten-a-long-condition), whose page shows a condition above the default limit,
and by [`ZA0602`](https://github.com/Nickelony/Stanza/wiki/ZA0602-Merge-nested-if-statements), which merges nested `if` statements only up to it.

| Value                                           | Effect                                                             |
| ----------------------------------------------- | ------------------------------------------------------------------ |
| `0`                                             | `ZA1005` reports no condition, and `ZA0602` merges without a bound |
| a number from 1 to 1000                         | a condition is reported when it holds more operators than that     |
| below zero, above 1000, absent, or not a number | the default                                                        |

At the default, four operators pass and a fifth is reported:

```csharp
// Unchanged - four operators
var canGrant = user.IsActive && user.HasRole(role) && !user.IsLocked && user.Age >= 18 && !user.IsBanned;

// Before - five operators
var canGrant = user.IsActive && user.HasRole(role) && !user.IsLocked && user.Age >= 18 && user.Region == region && !user.IsBanned;

// After
var canSignIn = user.IsActive && !user.IsLocked && !user.IsBanned;
var canGrant = canSignIn && user.HasRole(role) && user.Age >= 18 && user.Region == region;
```

At a limit of `1`, two operators are reported:

```csharp
// Before at a limit of 1 - two operators
var canGrant = user.IsActive && user.HasRole(role) && !user.IsLocked;

// After at a limit of 1
var isOpen = user.IsActive && !user.IsLocked;
var canGrant = isOpen && user.HasRole(role);
```

- The count covers one condition: parentheses and `!` keep it going, so `(a && b) || (c && d)` holds three operators.
  A lambda, an anonymous method or an argument starts it again, so `F(a && b, c && d)` is two conditions of one operator each.
- A pattern such as `x is 1 or 2 or 3` does not count, because it reads as a list.
