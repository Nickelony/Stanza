# ZA0042 - Use a block body for a multi-line value member

**Suggestion, off by default.**
Gives a property, indexer or accessor a block body when its signature or expression body spans more than one line.

```csharp
// Before
public override string Name =>
	First
			+ " "
			+ Last;

// After
public override string Name
{
	get
	{
		return First
			+ " "
			+ Last;
	}
}
```

- [`stanza_multiline_body_trigger`](https://github.com/Nickelony/Stanza/wiki/stanza_multiline_body_trigger) decides what makes the member multi-line,
  as it does for [`ZA0040`](https://github.com/Nickelony/Stanza/wiki/ZA0040-Use-a-block-body-for-a-multi-line-member).
- A property or indexer gets a `get` block around the body.
- A `get` or `set` accessor keeps its keyword, and a `set` block has no `return`.
- The expression text stays as written, so its wrapped lines keep their indentation.
- An accessor that shares its line with the property, as in `int Value { get => Build(`, is left as written.
- `ZA0043` judges only block bodies, so both can be [enabled](https://github.com/Nickelony/Stanza/wiki/Terms).
