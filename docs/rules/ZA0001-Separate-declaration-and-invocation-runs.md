# ZA0001 - Separate declaration and invocation runs

**Warning, on by default.**
Separates a [run](https://github.com/Nickelony/Stanza/wiki/Terms) of two or more neighboring declarations or invocations from its neighbors.

```csharp
// Before
var options = BuildOptions();
logger.LogInformation("Bound {Count} options.", options.Count);
logger.LogInformation("Starting the host.");
return options;

// After
var options = BuildOptions();

logger.LogInformation("Bound {Count} options.", options.Count);
logger.LogInformation("Starting the host.");

return options;
```

- Declarations group by kind: `const`, `var` or explicit type.
  Two neighboring explicit declarations are one run even when their types differ, such as `int` and `string`.
- Invocations group by the name of the method they call, and a call guarded by `?.` counts as an invocation.
- A local declaration, a field and an event field all count as a declaration.
- Assertion chains group by the root names that [`stanza_assertion_roots`](https://github.com/Nickelony/Stanza/wiki/stanza_assertion_roots) sets,
  not by method.
- A declaration on its own, directly above a run, also gets a blank line above it,
  unless it is the first unit of its container or a blank line already stands between it and the run.
