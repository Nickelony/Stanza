# ZA0020 - Keep single-line initializer members together

**Suggestion, off by default.**
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
  so it never shares a gap with the pair [`ZA0018`](https://github.com/Nickelony/Stanza/wiki/ZA0018-Separate-multi-line-initializer-members) and [`ZA0019`](https://github.com/Nickelony/Stanza/wiki/ZA0019-Keep-multi-line-initializer-members-together).
- A comment or directive in the gap follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
