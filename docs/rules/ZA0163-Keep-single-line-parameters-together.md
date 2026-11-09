# ZA0163 - Keep single-line parameters together

**Suggestion, on by default.**
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
  so it never shares a gap with the pair [`ZA0161`](https://github.com/Nickelony/Stanza/wiki/ZA0161-Separate-multi-line-parameters) and [`ZA0162`](https://github.com/Nickelony/Stanza/wiki/ZA0162-Keep-multi-line-parameters-together).
- The rule judges the same lists as [`ZA0161`](https://github.com/Nickelony/Stanza/wiki/ZA0161-Separate-multi-line-parameters), the elements of a tuple type among them.
- A comment or directive in the gap follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
