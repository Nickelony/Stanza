# ZA0652 - Use an equality operator for a null check

**Suggestion, off by default.**
Writes `x == null` and `x != null` for a pattern that only tests for `null`, wherever the comparison means the same.
The comparison can call an operator that the pattern skips, so the rule binds the operand and leaves a pattern alone where `==` would call one.

```csharp
// Before
static bool HasMessage(Exception error) => error is not null && error.Message is { };

// After
static bool HasMessage(Exception error) => error != null && error.Message != null;
```

A pattern that tests more than `null` stays as written:

```csharp
// Unchanged - the pattern also tests for an empty string
static bool IsBlank(string name) => name is null or "";
```

- The rule rewrites `is null`, `is not null`, `is { }` and `is not { }`.
  An empty property pattern matches any value that is not `null`, so `is { }` counts as a test for `null`.
- A pattern with a property, a type or a second test is left alone, and so are `is (null)` and `is { } found`, which declares a variable.
- The comparison binds looser than the pattern, so it takes parentheses where it is the right operand of `==` or `!=`,
  or an operand of a tighter operator or a pattern: `flag == value is null` becomes `flag == (value == null)`.
- A comment inside `is not` or inside `{ }` keeps the pattern as written.
- The operands that keep their form are those of [`ZA0651`](https://github.com/Nickelony/Stanza/wiki/ZA0651-Use-a-pattern-for-a-null-check):
  the pattern stays wherever the comparison would call an operator or would not mean the same.
- A non-nullable value type keeps the pattern too, because its comparison with `null` would test nothing.
- [`ZA0651`](https://github.com/Nickelony/Stanza/wiki/ZA0651-Use-a-pattern-for-a-null-check) writes the pattern instead.
  [The pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms), and a file that enables both gets `ZA0651`.
  Its page says how Roslynator's `RCS1248` relates to the pair.
