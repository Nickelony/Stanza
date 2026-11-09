# ZA0013 - Separate member groups

**Suggestion, on by default.**
Adds a blank line between a [run](https://github.com/Nickelony/Stanza/wiki/Terms) of two or more neighboring members of the same kind and the members beside it.

```csharp
// Before
public sealed class Widget
{
	private int _count;
	private string _name;
	public int Count => _count;
	public string Name => _name;
	public void Reset() => _count = 0;
}

// After
public sealed class Widget
{
	private int _count;
	private string _name;

	public int Count => _count;
	public string Name => _name;

	public void Reset() => _count = 0;
}
```

The blank line above `Count` ends the field run, so [`ZA0002`](https://github.com/Nickelony/Stanza/wiki/ZA0002-Separate-declaration-runs) writes it.
The blank line above `Reset` ends the property run, so this rule writes it.

A field that stands alone before a run of members gets a blank line on both sides:

```csharp
// Before
public void Setup() => Reset();
private int _count;
public int First => _count;
public int Second => _count;

// After
public void Setup() => Reset();

private int _count;

public int First => _count;
public int Second => _count;
```

- A change in member kind ends the run.
- A field that stands alone gets no blank line above it when a blank line already stands between it and the run.
- The [rules of a run of statements](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#one-run-of-statements-several-owners) own the [gaps](https://github.com/Nickelony/Stanza/wiki/Terms) around their runs,
  such as a run of fields or event fields for `ZA0002`;
  this rule skips those unless the rule that owns them is [off](https://github.com/Nickelony/Stanza/wiki/Terms) there.
- [`ZA0014`](https://github.com/Nickelony/Stanza/wiki/ZA0014-Separate-modifier-groups) and [`ZA0003`](https://github.com/Nickelony/Stanza/wiki/ZA0003-Separate-same-type-declaration-runs) leave a gap alone when one of those rules or this rule owns it.
- [`ZA0055`](https://github.com/Nickelony/Stanza/wiki/ZA0055-Keep-a-run-of-members-together) removes the blank lines inside a run of members of one kind where it is on.
