# ZA0015 - Keep single-line switch sections together

**Suggestion, off by default.**
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
  so it never shares a gap with the pair [`ZA0013`](https://github.com/Nickelony/Stanza/wiki/ZA0013-Separate-multi-line-switch-sections) and [`ZA0014`](https://github.com/Nickelony/Stanza/wiki/ZA0014-Keep-multi-line-switch-sections-together).
- A comment or directive in the gap follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
