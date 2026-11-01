# ZA0004 - Separate invocation runs

**Suggestion, on by default.**
Separates a [run](https://github.com/Nickelony/Stanza/wiki/Terms) of two or more neighboring calls of one method from its neighbors.

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

- Invocations group by the name of the method they call, and a call guarded by `?.` counts as an invocation.
  A discard such as `_ = Read();` is a call to `Read`.
- A chain is named by the call it starts from, so `Load().ConfigureAwait(false)` is a `Load` call and `First().ToArray()` a `First` call.
- A call whose chain holds a root that [`stanza_assertion_roots`](https://github.com/Nickelony/Stanza/wiki/stanza_assertion_roots) names is an assertion, so [`ZA0005`](https://github.com/Nickelony/Stanza/wiki/ZA0005-Separate-assertion-runs) judges it.
- Where [`ZA0006`](https://github.com/Nickelony/Stanza/wiki/ZA0006-Separate-await-runs) is on, a statement that awaits a call, such as `await Load();` or `_ = await Load();`,
  is an await statement whatever method it calls, as [One run of statements, several owners](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#one-run-of-statements-several-owners) says.
  Where `ZA0006` is [off](https://github.com/Nickelony/Stanza/wiki/Terms), it is a call to the method it awaits.
- A declaration on its own, directly above a run of calls, takes its blank line from this rule,
  and stays tight against the run while this rule is [off](https://github.com/Nickelony/Stanza/wiki/Terms).
- A gap where two runs meet goes to the rule with the lower ID, as [One run of statements, several owners](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#one-run-of-statements-several-owners) says.
- [`ZA0054`](https://github.com/Nickelony/Stanza/wiki/ZA0054-Keep-a-run-of-statements-together) removes the blank lines inside a run of calls where it is on.
