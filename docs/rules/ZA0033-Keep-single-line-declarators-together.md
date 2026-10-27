# ZA0033 - Keep single-line declarators together

**Suggestion, off by default.**
Removes the blank line when both declarators span one line.

```csharp
// Before
int first = 1,

	second = 2;

// After
int first = 1,
	second = 2;
```

- The rule judges only a [gap](https://github.com/Nickelony/Stanza/wiki/Terms) where both declarators span one line,
  so it never shares a gap with the pair [`ZA0031`](https://github.com/Nickelony/Stanza/wiki/ZA0031-Separate-multi-line-declarators) and [`ZA0032`](https://github.com/Nickelony/Stanza/wiki/ZA0032-Keep-multi-line-declarators-together).
- A comment or directive in the gap follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
