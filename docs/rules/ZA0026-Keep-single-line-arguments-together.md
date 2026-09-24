# ZA0026 - Keep single-line arguments together

**Suggestion, off by default.**
Removes the blank line when both arguments span one line.

```csharp
// Before
var options = ConfigurationSectionOptions.Create(
	"Application",

	validateOnLoad: true);

// After
var options = ConfigurationSectionOptions.Create(
	"Application",
	validateOnLoad: true);
```

- The rule judges only a [gap](https://github.com/Nickelony/Stanza/wiki/Terms) where both arguments span one line,
  so it never shares a gap with the pair [`ZA0024`](https://github.com/Nickelony/Stanza/wiki/ZA0024-Separate-multi-line-arguments) and [`ZA0025`](https://github.com/Nickelony/Stanza/wiki/ZA0025-Keep-multi-line-arguments-together).
- A comment or directive in the gap follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
