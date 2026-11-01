# ZA0161 - Separate multi-line parameters

**Suggestion, off by default.**
Separates two parameters when either of them spans more than one line.

```csharp
// Before
public static void Bind(
	string sectionName,
	IReadOnlyDictionary<
		string,
		ConfigurationSectionOptions> options)
{
	Configure(sectionName, options);
}

// After
public static void Bind(
	string sectionName,

	IReadOnlyDictionary<
		string,
		ConfigurationSectionOptions> options)
{
	Configure(sectionName, options);
}
```

- The rule inspects every parameter list in parentheses, such as a method's, a lambda's or a primary constructor's, and an indexer's brackets.
  It also inspects the elements of a tuple type such as `(int Id, string Name)`, wherever a type can stand.
- The rule leaves two single-line parameters as written.
- A comment or directive in the [gap](https://github.com/Nickelony/Stanza/wiki/Terms) follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
- The [tight rule](https://github.com/Nickelony/Stanza/wiki/Terms) is [`ZA0162`](https://github.com/Nickelony/Stanza/wiki/ZA0162-Keep-multi-line-parameters-together);
  [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
- The `strong` level enables the rule, and the `csharpier` formatter value turns it off, because
  [CSharpier undoes](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#layout-that-csharpier-undoes) the blank line it writes.
