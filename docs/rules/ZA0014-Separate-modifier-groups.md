# ZA0014 - Separate modifier groups

**Suggestion, off by default.**
Adds a blank line between a [run](https://github.com/Nickelony/Stanza/wiki/Terms) of two or more neighboring members of one kind
that share their access, `static` and `readonly` modifiers and the members beside it.

```csharp
// Before
public sealed class Session
{
	public int Id;
	public int Seed;
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
	public int Id;
	public int Seed;

	private static int s_count;
	private static int s_limit;

	private readonly int _id;
	private readonly int _seed;

	private int _attempts;
	private int _failures;
}
```

- Each mix of the three modifiers is its own group,
  so `public` fields never run with `private` ones, and `static readonly` fields never run with `static` or `readonly` ones.
- A member that writes no access runs with the members of the access its container gives it:
  `private` in a class or struct, `public` in an interface and `internal` in a namespace or a file.
- `protected internal` and `private protected` are each a group of their own.
- All parts of a partial type share one access, so a part that writes none takes the access of a part beside it in the same container that writes one.
  A part in another file or container is not read.
- The rule reads every member kind, so static methods run apart from instance methods.
- A `const` field already runs apart from other fields, because [`ZA0013`](https://github.com/Nickelony/Stanza/wiki/ZA0013-Separate-member-groups) counts it as a kind of its own.
- The other modifiers, such as `virtual` or `async`, do not end a run.
- The [rules of a run of statements](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#one-run-of-statements-several-owners) and `ZA0013` own the [gaps](https://github.com/Nickelony/Stanza/wiki/Terms) they report;
  this rule skips those unless that rule is [off](https://github.com/Nickelony/Stanza/wiki/Terms) there.
- [`ZA0003`](https://github.com/Nickelony/Stanza/wiki/ZA0003-Separate-same-type-declaration-runs) leaves a gap alone when this rule owns it.
- [`ZA0055`](https://github.com/Nickelony/Stanza/wiki/ZA0055-Keep-a-run-of-members-together) removes the blank lines inside a run of members that share their modifiers where it is on.
