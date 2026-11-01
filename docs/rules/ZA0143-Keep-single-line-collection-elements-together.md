# ZA0143 - Keep single-line collection elements together

**Suggestion, on by default.**
Removes the blank line when both elements span one line.

```csharp
// Before
var kinds = new[]
{
	ConfigurationSectionKind.Application,

	ConfigurationSectionKind.User,
};

// After
var kinds = new[]
{
	ConfigurationSectionKind.Application,
	ConfigurationSectionKind.User,
};
```

- The rule judges only a [gap](https://github.com/Nickelony/Stanza/wiki/Terms) where both collection elements span one line,
  so it never shares a gap with the pair [`ZA0141`](https://github.com/Nickelony/Stanza/wiki/ZA0141-Separate-multi-line-collection-elements) and [`ZA0142`](https://github.com/Nickelony/Stanza/wiki/ZA0142-Keep-multi-line-collection-elements-together).
- A comment or directive in the gap follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
