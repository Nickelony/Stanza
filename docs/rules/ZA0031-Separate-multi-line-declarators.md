# ZA0031 - Separate multi-line declarators

**Suggestion, off by default.**
Separates two declarators of one declaration when either of them spans more than one line.

```csharp
// Before
string sectionName = options.SectionName
		?? DefaultSectionName,
	displayName = options.DisplayName;

// After
string sectionName = options.SectionName
		?? DefaultSectionName,

	displayName = options.DisplayName;
```

- The rule leaves two single-line declarators as written, so `int a = 1, b = 2;` is never touched.
- A comment or directive in the [gap](https://github.com/Nickelony/Stanza/wiki/Terms) follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
- The [tight rule](https://github.com/Nickelony/Stanza/wiki/Terms) is [`ZA0032`](https://github.com/Nickelony/Stanza/wiki/ZA0032-Keep-multi-line-declarators-together);
  [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
- [Working with formatters](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#layout-that-csharpier-undoes) says why the rule is off by default.
