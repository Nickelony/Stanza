# ZA0012 - Separate multi-line switch sections

**Warning, on by default.**
Separates two sections of a `switch` statement when either spans more than one line.

```csharp
// Before
switch (state)
{
	case OrderState.Pending:
		logger.LogInformation("Waiting for the payment.");
		break;
	case OrderState.Shipped:
		logger.LogInformation("On its way.");
		break;
}

// After
switch (state)
{
	case OrderState.Pending:
		logger.LogInformation("Waiting for the payment.");
		break;

	case OrderState.Shipped:
		logger.LogInformation("On its way.");
		break;
}
```

- The rule leaves two single-line sections as written.
- A comment or directive in the [gap](https://github.com/Nickelony/Stanza/wiki/Terms) follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
- The [tight rule](https://github.com/Nickelony/Stanza/wiki/Terms) is [`ZA0013`](https://github.com/Nickelony/Stanza/wiki/ZA0013-Keep-multi-line-switch-sections-together);
  [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
