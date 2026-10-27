# ZA0010 - Separate multi-line accessors

**Suggestion, off by default.**
Separates two accessors when either of them spans more than one line.

```csharp
// Before
public int Value
{
	get
	{
		return _value;
	}
	set
	{
		_value = value;
	}
}

// After
public int Value
{
	get
	{
		return _value;
	}

	set
	{
		_value = value;
	}
}
```

- The rule leaves two single-line accessors as written, so `{ get; set; }` is never touched.
- A comment or directive in the [gap](https://github.com/Nickelony/Stanza/wiki/Terms) follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
- The [tight rule](https://github.com/Nickelony/Stanza/wiki/Terms) is [`ZA0011`](https://github.com/Nickelony/Stanza/wiki/ZA0011-Keep-multi-line-accessors-together);
  [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
- [Working with formatters](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#layout-that-csharpier-undoes) says why the rule is off by default.
