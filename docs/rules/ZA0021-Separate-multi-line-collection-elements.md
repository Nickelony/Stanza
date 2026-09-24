# ZA0021 - Separate multi-line collection elements

**Suggestion, off by default.**
Separates neighboring elements of an array, a collection expression, or a collection or complex element initializer
when either spans more than one line.

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

- The members of object, `with` and anonymous object initializers belong to `ZA0018`.
- The rule leaves two single-line elements as written.
- A comment or directive in the [gap](https://github.com/Nickelony/Stanza/wiki/Terms) follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
- The [tight rule](https://github.com/Nickelony/Stanza/wiki/Terms) is [`ZA0022`](https://github.com/Nickelony/Stanza/wiki/ZA0022-Keep-multi-line-collection-elements-together);
  [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
- [Working with formatters](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#layout-that-csharpier-undoes) says why the rule is off by default.
