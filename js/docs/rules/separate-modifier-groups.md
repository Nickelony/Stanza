# separate-modifier-groups

**Level: strong.**
Separates a run of two or more neighboring members of one kind that share their access, `static` and `readonly` modifiers
from the members beside it.

```ts
// Before
class Session {
	public id = 0;
	public seed = 0;
	private static count = 0;
	private static limit = 0;
	private readonly token = '';
	private readonly salt = '';
	private attempts = 0;
	private failures = 0;
}

// After
class Session {
	public id = 0;
	public seed = 0;

	private static count = 0;
	private static limit = 0;

	private readonly token = '';
	private readonly salt = '';

	private attempts = 0;
	private failures = 0;
}
```

- Each mix of the three modifiers is its own group, so `public` fields never run with `private` ones,
  and `static readonly` fields never run with `static` or `readonly` ones.
- A member with no access is public, and a `#name` member is private.
  A `protected` member is a group of its own.
- The rule reads every member kind, so `static` methods run apart from instance methods.
  An interface body and a type literal have no access or `static`, so only `readonly` splits their properties.
- The other modifiers, such as `abstract`, `override`, `declare` and `async`, and a `?` or decorator do not end a run.
- A getter and its setter, and the overload signatures of a method, stay together.
- The rule needs no option for the order of the groups, because a fix writes a blank line and never moves a member.
- [`separate-member-groups`](separate-member-groups.md) owns the gap between members of two kinds where both rules are enabled.
- [`keep-member-runs-together`](keep-member-runs-together.md) removes the blank lines inside a run of members that share their modifiers where it is also enabled.

## C# counterpart

[ZA0014 - Separate modifier groups](https://github.com/Nickelony/Stanza/wiki/ZA0014-Separate-modifier-groups).
