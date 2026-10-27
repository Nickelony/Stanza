# ZA0003 - Separate modifier groups

**Suggestion, off by default.**
Adds a blank line between a [run](https://github.com/Nickelony/Stanza/wiki/Terms) of two or more neighboring members of one kind
that share their `static` and `readonly` modifiers and the members beside it.

```csharp
// Before
public sealed class Session
{
	private static int s_count;
	private static int s_limit;
	private readonly int _id;
	private readonly int _seed;
	private int _attempts;
	private int _failures;
}

// After
public sealed class Session
{
	private static int s_count;
	private static int s_limit;

	private readonly int _id;
	private readonly int _seed;

	private int _attempts;
	private int _failures;
}
```

- Each mix of the two modifiers is its own group, so `static readonly` fields never run with `static` or `readonly` ones.
- The rule reads every member kind, so static methods run apart from instance methods.
- A `const` field already runs apart from other fields, because [`ZA0002`](https://github.com/Nickelony/Stanza/wiki/ZA0002-Separate-member-groups) counts it as a kind of its own.
- Access and the other modifiers, such as `virtual` or `async`, do not end a run.
- [`ZA0001`](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-declaration-and-invocation-runs) and `ZA0002` own the [gaps](https://github.com/Nickelony/Stanza/wiki/Terms) they report;
  this rule skips those unless that rule is [off](https://github.com/Nickelony/Stanza/wiki/Terms) there.
- [`ZA0004`](https://github.com/Nickelony/Stanza/wiki/ZA0004-Separate-same-type-declaration-groups) leaves a gap alone when this rule owns it.
