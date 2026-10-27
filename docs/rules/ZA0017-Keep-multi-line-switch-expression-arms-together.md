# ZA0017 - Keep multi-line switch expression arms together

**Suggestion, off by default.**
Removes the blank line between two arms when either of them spans more than one line.

```csharp
// Before
string status = state switch
{
	OrderState.Pending => Describe(
		state),

	OrderState.Shipped => "shipped",
};

// After
string status = state switch
{
	OrderState.Pending => Describe(
		state),
	OrderState.Shipped => "shipped",
};
```

- A comment or directive in the [gap](https://github.com/Nickelony/Stanza/wiki/Terms) follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
- The [separating rule](https://github.com/Nickelony/Stanza/wiki/Terms) is [`ZA0016`](https://github.com/Nickelony/Stanza/wiki/ZA0016-Separate-multi-line-switch-expression-arms);
  [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
