# ZA0002 - Separate declaration runs

**Suggestion, on by default.**
Separates a [run](https://github.com/Nickelony/Stanza/wiki/Terms) of two or more neighboring declarations of one kind from its neighbors.

```csharp
// Before
const int Limit = 3;
const int Retries = 5;
var options = BuildOptions();
var count = options.Count;
int total = count + Limit;
return total;

// After
const int Limit = 3;
const int Retries = 5;

var options = BuildOptions();
var count = options.Count;

int total = count + Limit;
return total;
```

- Declarations group by kind: `const`, `using`, `var` or explicit type.
  Two neighboring explicit declarations are one run even when their types differ, such as `int` and `string`.
- A `using` declaration is a kind of its own, with or without `await`, because taking a resource is a different job from declaring a value.
  So `using var stream = Open();` never runs with a `var` declaration, but two `using` declarations do.
- A local declaration, a field and an event field all count as a declaration,
  and so does a deconstruction that declares every name, such as `var (a, b) = Read();` or `(int a, string b) = Read();`.
- A deconstruction counts as `var` when it declares every name with `var`, and as an explicit type otherwise.
  One that writes an existing name, as `(int a, b) = Read();` does, is an assignment.
- A declaration on its own, directly above a run, takes its blank line from the rule of that run, this rule included,
  and stays tight against the run while that rule is [off](https://github.com/Nickelony/Stanza/wiki/Terms).
- A gap where two runs meet goes to the rule with the lower ID, as [One run of statements, several owners](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#one-run-of-statements-several-owners) says.
- [`ZA0054`](https://github.com/Nickelony/Stanza/wiki/ZA0054-Keep-a-run-of-statements-together) and [`ZA0055`](https://github.com/Nickelony/Stanza/wiki/ZA0055-Keep-a-run-of-members-together) remove the blank lines inside a run of declarations where they are on.
