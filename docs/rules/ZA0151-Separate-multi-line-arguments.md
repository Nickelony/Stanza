# ZA0151 - Separate multi-line arguments

**Suggestion, off by default.**
Separates two arguments in an argument list when either spans more than one line,
so a multi-line argument, such as a lambda, a wrapped object initializer or a call chain,
sits apart from its neighbor.

```csharp
// Before
var options = ConfigurationSectionOptions.Create(
	"Application",
	validateOnLoad: true,
	onInvalid: (key, value) =>
	{
		logger.LogWarning("Invalid value for {Key}: {Value}", key, value);
	});

// After
var options = ConfigurationSectionOptions.Create(
	"Application",
	validateOnLoad: true,

	onInvalid: (key, value) =>
	{
		logger.LogWarning("Invalid value for {Key}: {Value}", key, value);
	});
```

- The rule inspects argument lists of calls, `new`, constructor initializers, attributes and indexers,
  and the elements of a tuple literal such as `(id, start, end)`.
- The rule leaves two single-line arguments as written.
- A comment or directive in the [gap](https://github.com/Nickelony/Stanza/wiki/Terms) follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
- The [tight rule](https://github.com/Nickelony/Stanza/wiki/Terms) is [`ZA0152`](https://github.com/Nickelony/Stanza/wiki/ZA0152-Keep-multi-line-arguments-together);
  [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
- The `strong` level enables the rule, and the `csharpier` formatter value turns it off, because
  [CSharpier undoes](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#layout-that-csharpier-undoes) the blank line it writes.
