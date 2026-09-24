# ZA0042 - Use an expression body for a single-line value member

**Suggestion, off by default.**
Gives a property, indexer or accessor an expression body when its signature and single statement fit within the line width.

```csharp
// Before
public int Count
{
	get { return _items.Count; }
}

// After
public int Count => _items.Count;
```

- A property or indexer whose only accessor is a getter becomes one expression-bodied member,
  unless the property has an initializer, which an expression-bodied member cannot take.
- Otherwise, each accessor whose body is one statement becomes an expression body,
  such as `get => _items.Count;`.
- [`stanza_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_max_line_length) decides whether the member fits,
  as it does for [`ZA0040`](https://github.com/Nickelony/Stanza/wiki/ZA0040-Use-an-expression-body-for-a-single-line-member).
- A member that fits only with the expression on its own line gets that shape,
  and [`stanza_multiline_body_trigger`](https://github.com/Nickelony/Stanza/wiki/stanza_multiline_body_trigger) decides whether the arrow counts, the same as for `ZA0040`.
- A `with` expression or a query keeps its block where only that shape fits and the trigger counts the body, as for `ZA0040`.
- `ZA0041` judges only expression bodies, so both can be [enabled](https://github.com/Nickelony/Stanza/wiki/Terms).
- A project below C# 6.0 gets no report, and an accessor or a body that throws needs C# 7.0, as for `ZA0040`.
