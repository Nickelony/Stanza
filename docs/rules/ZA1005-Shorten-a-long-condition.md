# ZA1005 - Shorten a long condition

**Suggestion, off by default.**
Reports a condition that holds more `&&` and `||` operators than
[`stanza_max_logical_operators`](https://github.com/Nickelony/Stanza/wiki/stanza_max_logical_operators) allows.
It is a [report-only rule](https://github.com/Nickelony/Stanza/wiki/Terms), so it has no code fix.

```csharp
// Before - five operators, so a reader weighs six tests at once
if (
	user.IsActive
	&& user.HasRole(role)
	&& !user.IsLocked
	&& user.Age >= 18
	&& user.Region == region
	&& !user.IsBanned)
{
	Grant(user);
}

// After - rewritten by hand, with the tests grouped under names
var canSignIn = user.IsActive && !user.IsLocked && !user.IsBanned;
var isEligible = user.HasRole(role) && user.Age >= 18 && user.Region == region;

if (canSignIn && isEligible)
{
	Grant(user);
}
```

- The rule reports once for a condition, at its outermost `&&` or `||`, wherever the condition stands:
  in an `if` or a `while`, a `return`, an assignment or a conditional.
- [`stanza_max_logical_operators`](https://github.com/Nickelony/Stanza/wiki/stanza_max_logical_operators) says what one condition covers, such as parentheses, a lambda and a pattern.
- The method `Equals` and the `==` and `!=` operators are skipped, because a long field-by-field comparison reads fine.
- [`ZA0602`](https://github.com/Nickelony/Stanza/wiki/ZA0602-Merge-nested-if-statements) merges nested `if` statements only up to the same limit,
  so it never writes a condition that this rule reports.
- The message asks for the parts of the condition to move into named locals or a method, but the rule has no fix, because each part needs a name.
