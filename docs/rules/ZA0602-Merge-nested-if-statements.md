# ZA0602 - Merge nested if statements

**Suggestion, off by default.**
Merges an `if` that holds only another `if` into one condition joined by `&&`,
as long as the result stays within [`stanza_max_logical_operators`](https://github.com/Nickelony/Stanza/wiki/stanza_max_logical_operators).

```csharp
// Before
if (user is not null)
{
	if (user.IsActive)
	{
		Grant(user);
	}
}

// After
if (user is not null && user.IsActive)
{
	Grant(user);
}
```

An operand that `&&` binds tighter than takes parentheses:

```csharp
// Before
if (isAdmin || isOwner)
{
	if (isActive)
	{
		Grant(user);
	}
}

// After
if ((isAdmin || isOwner) && isActive)
{
	Grant(user);
}
```

A merge that would pass the limit stays nested:

```csharp
// Unchanged - five operators, past the default limit
if (a && b && c)
{
	if (d && e && f)
	{
		Run();
	}
}
```

- Both `if` statements must have no `else`, and the inner one must be the only statement of the outer body, braced or not.
- A chain of three or more merges as far as the limit allows, in one fix, and the rule reports it once, at the outermost `if`.
- An `if` that a merge of the `if` around it takes in is not reported on its own.
- The merged condition counts the operators of both conditions and the `&&` that joins them, as
  [`ZA1005`](https://github.com/Nickelony/Stanza/wiki/ZA1005-Shorten-a-long-condition) does,
  so the rule never writes a condition that one reports.
- A limit of `0` lets a merge grow without bound, and so does a merge inside an `Equals` method or an `==` or `!=` operator, which `ZA1005` skips.
- An operand that holds `||`, `??`, `?:`, an assignment, a lambda, an anonymous method or a query at its top takes parentheses.
  An operand that already binds as tightly as `&&` does not.
- A comment or a directive between the two heads keeps the `if` statements apart, as it would be left without the line it belongs to.
  A comment elsewhere, in the body or after a closing brace, stays on a line.
- A variable that the inner condition declares joins the scope around the outer `if`.
  The `if` statements stay apart when that scope reads its name elsewhere, in the outer condition or in disabled text included.
- The fix moves the body out one indent step for each head the merge loses.
  It treats the token after the closing brace, a directive in or around the statement and the order of the [`ZA0205`](https://github.com/Nickelony/Stanza/wiki/ZA0205-Use-braces-consistently) and [`ZA0206`](https://github.com/Nickelony/Stanza/wiki/ZA0206-Remove-redundant-braces) fixes
  as [`ZA0601`](https://github.com/Nickelony/Stanza/wiki/ZA0601-Remove-the-else-after-a-jump) does.
- The fix writes the blank lines [`ZA0001`](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-multi-line-constructs) asks for around the pair,
  as [A blank line a fix leaves behind](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-blank-line-a-fix-leaves-behind) says.
- A condition that is not a `bool` keeps its `if` apart from the one around it and the one inside it.
  A type that defines `true` and `false` but no `&` is a valid condition and no valid operand of `&&`.
- The `if` statements before such a condition and the ones inside it merge as far as their own conditions allow.
  The ones inside wait for the merge around them, which moves their lines.
- The rule binds the conditions to read their types, and one it cannot bind counts as a `bool`.
- Roslynator offers the same change as `RCS1061`, hidden by default.
