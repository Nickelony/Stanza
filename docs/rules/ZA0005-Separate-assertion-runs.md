# ZA0005 - Separate assertion runs

**Suggestion, on by default.**
Separates a [run](https://github.com/Nickelony/Stanza/wiki/Terms) of two or more neighboring assertions that share a root from its neighbors,
so the arrange, act and assert steps of a test read as three stanzas.

```csharp
// Before
var result = Parse(text);
Assert.That(result.Count, Is.EqualTo(2));
Assert.That(result.First(), Is.Not.Null);
Cleanup();

// After
var result = Parse(text);

Assert.That(result.Count, Is.EqualTo(2));
Assert.That(result.First(), Is.Not.Null);

Cleanup();
```

- A call is an assertion when its callee holds a name that [`stanza_assertion_roots`](https://github.com/Nickelony/Stanza/wiki/stanza_assertion_roots) lists, such as `Assert` in `Assert.That(...)`.
- Assertions group by that root, not by method, so `Assert.That(...)` and `Assert.AreEqual(...)` are one run.
- A declaration on its own, directly above a run of assertions, takes its blank line from this rule,
  and stays tight against the run while this rule is [off](https://github.com/Nickelony/Stanza/wiki/Terms).
- A gap where two runs meet goes to the rule with the lower ID, as [One run of statements, several owners](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#one-run-of-statements-several-owners) says.
- With this rule off, a run of assertions stays tight, and [`ZA0004`](https://github.com/Nickelony/Stanza/wiki/ZA0004-Separate-invocation-runs) does not take it over.
- An awaited assertion, such as `await Assert.ThrowsAsync<T>(Act);`, stays an assertion
  where [`ZA0006`](https://github.com/Nickelony/Stanza/wiki/ZA0006-Separate-await-runs) is on.
- [`ZA0054`](https://github.com/Nickelony/Stanza/wiki/ZA0054-Keep-a-run-of-statements-together) removes the blank lines inside a run of assertions where it is on.
