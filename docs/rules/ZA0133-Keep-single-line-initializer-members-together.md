# ZA0133 - Keep single-line initializer members together

**Suggestion, on by default.**
Removes the blank line when both members span one line.

```csharp
// Before
var item = new Item
{
	Name = "a",

	Count = 3,
};

// After
var item = new Item
{
	Name = "a",
	Count = 3,
};
```

- The rule judges only a [gap](https://github.com/Nickelony/Stanza/wiki/Terms) where both initializer members span one line,
  so it never shares a gap with the pair [`ZA0131`](https://github.com/Nickelony/Stanza/wiki/ZA0131-Separate-multi-line-initializer-members) and [`ZA0132`](https://github.com/Nickelony/Stanza/wiki/ZA0132-Keep-multi-line-initializer-members-together).
- A comment or directive in the gap follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
