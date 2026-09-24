# ZA0010 - Keep multi-line accessors together

**Suggestion, off by default.**
Removes the blank line between two accessors when either of them spans more than one line.

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

- A comment or directive in the [gap](https://github.com/Nickelony/Stanza/wiki/Terms) follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
- The [separating rule](https://github.com/Nickelony/Stanza/wiki/Terms) is [`ZA0009`](https://github.com/Nickelony/Stanza/wiki/ZA0009-Separate-multi-line-accessors);
  [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
