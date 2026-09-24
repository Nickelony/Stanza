# ZA0002 - Separate member groups

**Warning, on by default.**
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

The blank line above `Count` ends the field run, so [`ZA0001`](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-declaration-and-invocation-runs) writes it.
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
- `ZA0001` owns the [gaps](https://github.com/Nickelony/Stanza/wiki/Terms) around a run of declarations, such as fields and event fields;
  this rule skips those unless `ZA0001` is [off](https://github.com/Nickelony/Stanza/wiki/Terms) there.
- [`ZA0003`](https://github.com/Nickelony/Stanza/wiki/ZA0003-Separate-same-type-declaration-groups) leaves a gap alone when `ZA0001` or this rule owns it.
