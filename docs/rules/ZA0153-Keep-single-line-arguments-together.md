# ZA0153 - Keep single-line arguments together

**Suggestion, on by default.**
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
  so it never shares a gap with the pair [`ZA0151`](https://github.com/Nickelony/Stanza/wiki/ZA0151-Separate-multi-line-arguments) and [`ZA0152`](https://github.com/Nickelony/Stanza/wiki/ZA0152-Keep-multi-line-arguments-together).
- The rule judges the same lists as [`ZA0151`](https://github.com/Nickelony/Stanza/wiki/ZA0151-Separate-multi-line-arguments), the elements of a tuple literal among them.
- A comment or directive in the gap follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
