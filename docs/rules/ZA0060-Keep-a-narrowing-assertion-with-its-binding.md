# ZA0060 - Keep a narrowing assertion with its binding

**Suggestion, off by default.**
Removes the blank line between a statement that binds a name and the single-line assertion below it that narrows that name.

```csharp
// Before
Audit(id);
Customer? customer = repository.Find(id);

System.Diagnostics.Debug.Assert(customer is not null);
Show(customer);

// After
Audit(id);
Customer? customer = repository.Find(id);
System.Diagnostics.Debug.Assert(customer is not null);
Show(customer);

// Unchanged - the assertion checks a value, as the assert phase of a test does
Audit(id);
int count = repository.Count();

System.Diagnostics.Debug.Assert(count == 3);
Show(count);
```

- An assertion that narrows the name the statement above binds is there for the compiler's flow analysis,
  so it reads as part of the binding.
- A bound name passed to a `[NotNull]` parameter narrows, with or without `!`,
  as in `ArgumentNullException.ThrowIfNull(name)` or an `Assert.NotNull(name)` that marks its parameter.
- A check passed to a `[DoesNotReturnIf(false)]` parameter narrows, as in `Debug.Assert(name != null)`:
  `name != null`, `name is not null`, `name is T`, `name is { }`, or an `&&` that holds one of them.
- A null check passed to a `[DoesNotReturnIf(true)]` parameter narrows:
  `name == null`, `name is null`, or an `||` that holds one of them.
- Any other check, such as `Assert.Equal(3, count)` or `Debug.Assert(count == 3)`, checks a value,
  so the blank line that sets the assert phase of a test apart stays.
- A phase that mixes narrowing and ordinary assertions keeps its blank line above.
  A group made only of single-line narrowing assertions can attach the contiguous bindings it checks, whether or not [`ZA0012`](https://github.com/Nickelony/Stanza/wiki/ZA0012-Separate-a-checked-stanza) is on.
- The binding is a local declaration, a deconstruction included, or a statement that assigns a name with `=`, and it may span several lines.
  An assertion that spans several lines keeps the blank line.
- A comment or a directive in the gap, a trailing comment included, keeps the blank line.
- The rule owns the gap it keeps tight, so the separating rules ask for no blank line there, as [A keep rule and the other owners of its gap](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-keep-rule-and-the-other-owners-of-its-gap) says.
- The rule judges the gap above the assertion only, in a block, a switch section or at the top level of a file.
  The gap below it belongs to the other rules.
- A boundary that a separating rule asks for inside the kept group moves above it, as [The boundary of a kept group](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#the-boundary-of-a-kept-group) says.
