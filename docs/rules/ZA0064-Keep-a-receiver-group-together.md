# ZA0064 - Keep a receiver group together

**Suggestion, off by default.**
Removes blank lines inside an eligible group of operations through a selected receiver.

With [`stanza_receiver_roots`](https://github.com/Nickelony/Stanza/wiki/stanza_receiver_roots) set to `db`:

```csharp
// Before
db.Add(cart);

await db.Flush();
await db.Refresh(cart);

// After
db.Add(cart);
await db.Flush();
await db.Refresh(cart);
```

- Direct calls, awaited calls, member assignments, compound assignments and updates can share a group.
  [`stanza_receiver_comparison`](https://github.com/Nickelony/Stanza/wiki/stanza_receiver_comparison) chooses whether receivers compare by root or by full path.
- The rule works without [`ZA0004`](https://github.com/Nickelony/Stanza/wiki/ZA0004-Separate-invocation-runs) or [`ZA0055`](https://github.com/Nickelony/Stanza/wiki/ZA0055-Keep-a-run-of-statements-together), and a receiver that the setting does not select is left to the other rules.
- Multi-line operations, assertions, guards, unsupported paths, comments and directives end groups.
  Consecutive operations must occupy separate physical lines.
  Declarations and bare local assignments stay outside the group.
- A blank line between two [sections](https://github.com/Nickelony/Stanza/wiki/Terms) of a group stays as written, even where [`ZA0052`](https://github.com/Nickelony/Stanza/wiki/ZA0052-Gather-a-run-of-loose-statements) or [`ZA0055`](https://github.com/Nickelony/Stanza/wiki/ZA0055-Keep-a-run-of-statements-together) would remove it.
  A lone operation beside a section joins it, and one fix removes every eligible blank line of its group.
- Inside a group the rule wins over the splits of the assignment and invocation runs.
- The rule asks for no blank line around a group; enable [`ZA0010`](https://github.com/Nickelony/Stanza/wiki/ZA0010-Separate-receiver-groups) to separate receiver groups as well.
- [`ZA0062`](https://github.com/Nickelony/Stanza/wiki/ZA0062-Keep-an-initialization-with-its-calls) keeps an initialization with its first direct call independently.
- A boundary that a separating rule asks for inside the group moves above it, as [The boundary of a kept group](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#the-boundary-of-a-kept-group) says.
