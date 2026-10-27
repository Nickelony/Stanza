# ZA0026 - Keep multi-line arguments together

**Suggestion, off by default.**
Removes the blank line between two arguments when either of them spans more than one line.

```csharp
// Before
var options = ConfigurationSectionOptions.Create(
	"Application",

	onInvalid: (key, value) =>
	{
		logger.LogWarning("Invalid value for {Key}: {Value}", key, value);
	});

// After
var options = ConfigurationSectionOptions.Create(
	"Application",
	onInvalid: (key, value) =>
	{
		logger.LogWarning("Invalid value for {Key}: {Value}", key, value);
	});
```

- A comment or directive in the [gap](https://github.com/Nickelony/Stanza/wiki/Terms) follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
- The [separating rule](https://github.com/Nickelony/Stanza/wiki/Terms) is [`ZA0025`](https://github.com/Nickelony/Stanza/wiki/ZA0025-Separate-multi-line-arguments);
  [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
