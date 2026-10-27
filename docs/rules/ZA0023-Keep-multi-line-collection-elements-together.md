# ZA0023 - Keep multi-line collection elements together

**Suggestion, off by default.**
Removes the blank line between two elements when either of them spans more than one line.

```csharp
// Before
var handlers = new IHandler[]
{
	new ConfigurationHandler(
		ConfigurationSectionKind.Application),

	new ValidationHandler(
		ConfigurationSectionKind.Application),
};

// After
var handlers = new IHandler[]
{
	new ConfigurationHandler(
		ConfigurationSectionKind.Application),
	new ValidationHandler(
		ConfigurationSectionKind.Application),
};
```

- A comment or directive in the [gap](https://github.com/Nickelony/Stanza/wiki/Terms) follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
- The [separating rule](https://github.com/Nickelony/Stanza/wiki/Terms) is [`ZA0022`](https://github.com/Nickelony/Stanza/wiki/ZA0022-Separate-multi-line-collection-elements);
  [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
