# ZA0029 - Keep single-line parameters together

**Suggestion, off by default.**
Removes the blank line when both parameters span one line.

```csharp
// Before
public void Register(
	int capacity,

	int retries)
{
	Connect(capacity, retries);
}

// After
public void Register(
	int capacity,
	int retries)
{
	Connect(capacity, retries);
}
```

- The rule judges only a [gap](https://github.com/Nickelony/Stanza/wiki/Terms) where both parameters span one line,
  so it never shares a gap with the pair [`ZA0027`](https://github.com/Nickelony/Stanza/wiki/ZA0027-Separate-multi-line-parameters) and [`ZA0028`](https://github.com/Nickelony/Stanza/wiki/ZA0028-Keep-multi-line-parameters-together).
- A comment or directive in the gap follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
