# ZA0028 - Keep multi-line parameters together

**Suggestion, off by default.**
Removes the blank line between two parameters when either of them spans more than one line.

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

- A comment or directive in the [gap](https://github.com/Nickelony/Stanza/wiki/Terms) follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
- The [separating rule](https://github.com/Nickelony/Stanza/wiki/Terms) is [`ZA0027`](https://github.com/Nickelony/Stanza/wiki/ZA0027-Separate-multi-line-parameters);
  [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
