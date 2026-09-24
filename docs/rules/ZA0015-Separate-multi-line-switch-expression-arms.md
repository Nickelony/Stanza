# ZA0015 - Separate multi-line switch expression arms

**Warning, on by default.**
Separates two arms of a `switch` expression when either spans more than one line.

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

- The rule leaves two single-line arms as written.
- A comment or directive in the [gap](https://github.com/Nickelony/Stanza/wiki/Terms) follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
- The [tight rule](https://github.com/Nickelony/Stanza/wiki/Terms) is [`ZA0016`](https://github.com/Nickelony/Stanza/wiki/ZA0016-Keep-multi-line-switch-expression-arms-together);
  [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
