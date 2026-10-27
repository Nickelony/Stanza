# ZA0024 - Keep single-line collection elements together

**Suggestion, off by default.**
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
  so it never shares a gap with the pair [`ZA0022`](https://github.com/Nickelony/Stanza/wiki/ZA0022-Separate-multi-line-collection-elements) and [`ZA0023`](https://github.com/Nickelony/Stanza/wiki/ZA0023-Keep-multi-line-collection-elements-together).
- A comment or directive in the gap follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
