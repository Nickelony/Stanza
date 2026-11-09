# ZA0006 - Separate await runs

**Suggestion, off by default.**
Separates a [run](https://github.com/Nickelony/Stanza/wiki/Terms) of two or more neighboring await statements from its neighbors.

```csharp
// Before
var client = CreateClient();
await client.ConnectAsync();
_ = await client.SendAsync(request);
Log("Sent.");

// After
var client = CreateClient();

await client.ConnectAsync();
_ = await client.SendAsync(request);

Log("Sent.");
```

- An await statement awaits, as `await SaveAsync();` does, or discards what it awaits, as `_ = await ReadAsync();` does.
  Await statements are one kind whatever they await, so `await Task.Delay(100);` and `await task.ConfigureAwait(false);` join them.
- `var data = await ReadAsync();` is a declaration that [`ZA0002`](https://github.com/Nickelony/Stanza/wiki/ZA0002-Separate-declaration-runs) judges,
  and `data = await ReadAsync();` is an assignment that [`ZA0008`](https://github.com/Nickelony/Stanza/wiki/ZA0008-Separate-assignment-runs) judges.
- An awaited call whose chain holds a root that [`stanza_assertion_roots`](https://github.com/Nickelony/Stanza/wiki/stanza_assertion_roots) names stays an assertion,
  so [`ZA0005`](https://github.com/Nickelony/Stanza/wiki/ZA0005-Separate-assertion-runs) judges `await Assert.ThrowsAsync<T>(Act);`.
- An `await foreach` or an `await using` statement is no await statement.
- Where this rule is on, an awaited call is an await statement whatever method it calls,
  so [`ZA0004`](https://github.com/Nickelony/Stanza/wiki/ZA0004-Separate-invocation-runs) leaves it out of the runs of calls of that method.
  Where this rule is [off](https://github.com/Nickelony/Stanza/wiki/Terms), `ZA0004` groups awaited calls by the method they call.
- A lone await statement stays tight, as a lone call does.
- A declaration on its own, directly above a run of await statements, takes its blank line from this rule,
  and stays tight against the run while this rule is [off](https://github.com/Nickelony/Stanza/wiki/Terms).
- A gap where two runs meet goes to the rule with the lower ID, as [One run of statements, several owners](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#one-run-of-statements-several-owners) says.
- [`ZA0054`](https://github.com/Nickelony/Stanza/wiki/ZA0054-Keep-a-run-of-statements-together) removes the blank lines inside a run of await statements where it is on.
