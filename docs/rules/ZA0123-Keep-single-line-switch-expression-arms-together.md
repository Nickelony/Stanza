# ZA0123 - Keep single-line switch expression arms together

**Suggestion, on by default.**
Removes the blank line when both arms span one line.

```csharp
// Before
string status = state switch
{
	OrderState.Pending => "waiting",

	OrderState.Shipped => "shipped",
};

// After
string status = state switch
{
	OrderState.Pending => "waiting",
	OrderState.Shipped => "shipped",
};
```

- The rule judges only a [gap](https://github.com/Nickelony/Stanza/wiki/Terms) where both switch expression arms span one line,
  so it never shares a gap with the pair [`ZA0121`](https://github.com/Nickelony/Stanza/wiki/ZA0121-Separate-multi-line-switch-expression-arms) and [`ZA0122`](https://github.com/Nickelony/Stanza/wiki/ZA0122-Keep-multi-line-switch-expression-arms-together).
- A comment or directive in the gap follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
