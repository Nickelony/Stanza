# ZA0018 - Separate multi-line initializer members

**Suggestion, off by default.**
Separates two members of an object initializer, a `with` initializer or an anonymous object initializer when either spans more than one line.

```csharp
// Before
var item = new Item
{
	Name = "a",
	Tags = new List<string>
	{
		"x",
	},
	Count = 3,
};

// After
var item = new Item
{
	Name = "a",

	Tags = new List<string>
	{
		"x",
	},

	Count = 3,
};
```

- [`ZA0021`](https://github.com/Nickelony/Stanza/wiki/ZA0021-Separate-multi-line-collection-elements) covers the elements of arrays, collection expressions, and collection and complex element initializers.
- The rule leaves two single-line members as written.
- A comment or directive in the [gap](https://github.com/Nickelony/Stanza/wiki/Terms) follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
- The [tight rule](https://github.com/Nickelony/Stanza/wiki/Terms) is [`ZA0019`](https://github.com/Nickelony/Stanza/wiki/ZA0019-Keep-multi-line-initializer-members-together);
  [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
