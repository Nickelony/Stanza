# ZA0034 - Keep clause keywords adjacent

**Suggestion, on by default.**
Removes the blank line between a closing brace and the clause keyword that continues the statement.

```csharp
// Before
try
{
	options = Bind();
}

catch (ConfigurationException exception)
{
	logger.LogError(exception, "Binding failed.");
}

// After
try
{
	options = Bind();
}
catch (ConfigurationException exception)
{
	logger.LogError(exception, "Binding failed.");
}
```

- The rule covers the clause keywords that [`ZA0033`](https://github.com/Nickelony/Stanza/wiki/ZA0033-Separate-a-clause-keyword-from-the-brace-before-it) lists.
- A blank line between a clause header and its opening brace belongs to `ZA0035`.
- A branch without braces has no closing brace, so it is left alone.
- With a comment in the [gap](https://github.com/Nickelony/Stanza/wiki/Terms), the blank lines before the comment go,
  and a directive in the gap keeps it.
- The [separating rule](https://github.com/Nickelony/Stanza/wiki/Terms) is [`ZA0033`](https://github.com/Nickelony/Stanza/wiki/ZA0033-Separate-a-clause-keyword-from-the-brace-before-it);
  [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
