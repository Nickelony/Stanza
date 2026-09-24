# ZA0031 - Keep multi-line declarators together

**Suggestion, off by default.**
Removes the blank line between two declarators when either of them spans more than one line.

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

- A comment or directive in the [gap](https://github.com/Nickelony/Stanza/wiki/Terms) follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
- The [separating rule](https://github.com/Nickelony/Stanza/wiki/Terms) is [`ZA0030`](https://github.com/Nickelony/Stanza/wiki/ZA0030-Separate-multi-line-declarators);
  [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
