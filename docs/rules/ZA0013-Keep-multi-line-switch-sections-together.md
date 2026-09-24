# ZA0013 - Keep multi-line switch sections together

**Suggestion, off by default.**
Removes the blank line between two sections when either of them spans more than one line.

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

- A comment or directive in the [gap](https://github.com/Nickelony/Stanza/wiki/Terms) follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
- The [separating rule](https://github.com/Nickelony/Stanza/wiki/Terms) is [`ZA0012`](https://github.com/Nickelony/Stanza/wiki/ZA0012-Separate-multi-line-switch-sections);
  [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
