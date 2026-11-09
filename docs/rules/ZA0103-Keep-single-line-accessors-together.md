# ZA0103 - Keep single-line accessors together

**Suggestion, on by default.**
Removes the blank line when both accessors span one line.

```csharp
// Before
public int Value
{
	get { return _value; }

	set { _value = value; }
}

// After
public int Value
{
	get { return _value; }
	set { _value = value; }
}
```

- The rule judges only a [gap](https://github.com/Nickelony/Stanza/wiki/Terms) where both accessors span one line,
  so it never shares a gap with the pair [`ZA0101`](https://github.com/Nickelony/Stanza/wiki/ZA0101-Separate-multi-line-accessors) and [`ZA0102`](https://github.com/Nickelony/Stanza/wiki/ZA0102-Keep-multi-line-accessors-together).
- A comment or directive in the gap follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
