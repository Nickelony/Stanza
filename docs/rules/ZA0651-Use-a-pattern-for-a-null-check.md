# ZA0651 - Use a pattern for a null check

**Suggestion, on by default.**
Writes `x is null` and `x is not null` for `x == null` and `x != null`, wherever the pattern means the same.
A pattern calls no operator, so the rule binds the operand and leaves a comparison alone where `==` could do more than test for `null`.

```csharp
// Before
static string Describe(Exception error)
{
	if (error == null)
		return "none";

	return null != error.InnerException ? error.InnerException.Message : error.Message;
}

// After
static string Describe(Exception error)
{
	if (error is null)
		return "none";

	return error.InnerException is not null ? error.InnerException.Message : error.Message;
}
```

A type that declares its own `==` keeps the comparison, because the pattern would skip the operator:

```csharp
// Unchanged - Name counts an empty name as null, which the pattern would not
class Name
{
	public string Text = "";

	public static bool operator ==(Name left, Name right) => (left?.Text ?? "") == (right?.Text ?? "");
	public static bool operator !=(Name left, Name right) => !(left == right);

	static bool IsBlank(Name name) => name == null;
}
```

- `null == x` turns into `x is null` too, unless a comment stands between the two operands.
  A parenthesized `(null)` keeps the comparison.
- An equality on the left takes parentheses, so `a == b == null` becomes `(a == b) is null`.
- `is null` needs C# 7 and `is not null` C# 9, and an operand that is a pointer or a type parameter takes either only from C# 8.
  The rule reads the language version of the project and leaves a comparison the version cannot write.
- A comparison stays as written where the operand's type, a base class or the constraint of a type parameter declares `==` or `!=`.
  That includes a static abstract operator.
- `string`, a delegate, a record with generated equality, a nullable value type and a pointer are rewritten,
  because their `==` against `null` only tests for it.
  The operator of a nullable struct lifts, and the lifted form answers `null` without calling it.
- A record that declares its own `Equals` keeps the comparison, because its generated `==` calls that `Equals`.
- A non-nullable value type keeps the comparison, because `==` there always answers `false` and `is null` does not compile.
  So do `dynamic`, a constant, two `null` literals and an operand that does not bind.
- A comparison inside an expression tree stays, because an expression tree holds no pattern.
  That includes a lambda passed to `IQueryable` and a query clause over one,
  but not the inner sequence of a query's `join`, which is an ordinary argument.
- An operator, or a record's `Equals`, in text that another [view](https://github.com/Nickelony/Stanza/wiki/Terms) compiles keeps the comparison in every view.
- [`ZA0652`](https://github.com/Nickelony/Stanza/wiki/ZA0652-Use-an-equality-operator-for-a-null-check) writes the comparison instead.
  This rule is the [default half](https://github.com/Nickelony/Stanza/wiki/Terms) of the pair, and it owns the check where a file enables both.
  [The pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) lists the pair.
- Roslynator's `RCS1248` makes the same change in either direction, so a project enables one of the two.
