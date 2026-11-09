# ZA0113 - Keep single-line switch sections together

**Suggestion, on by default.**
Removes the blank line when both sections span one line.

```csharp
// Before
switch (state)
{
	case OrderState.Pending: return "waiting";

	case OrderState.Shipped: return "shipped";
}

// After
switch (state)
{
	case OrderState.Pending: return "waiting";
	case OrderState.Shipped: return "shipped";
}
```

- The rule judges only a [gap](https://github.com/Nickelony/Stanza/wiki/Terms) where both switch sections span one line,
  so it never shares a gap with the pair [`ZA0111`](https://github.com/Nickelony/Stanza/wiki/ZA0111-Separate-multi-line-switch-sections) and [`ZA0112`](https://github.com/Nickelony/Stanza/wiki/ZA0112-Keep-multi-line-switch-sections-together).
- A comment or directive in the gap follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
