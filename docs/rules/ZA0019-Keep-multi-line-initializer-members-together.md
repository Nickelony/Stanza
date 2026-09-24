# ZA0019 - Keep multi-line initializer members together

**Suggestion, off by default.**
Removes the blank line between two members when either of them spans more than one line.

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

- A comment or directive in the [gap](https://github.com/Nickelony/Stanza/wiki/Terms) follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
- The [separating rule](https://github.com/Nickelony/Stanza/wiki/Terms) is [`ZA0018`](https://github.com/Nickelony/Stanza/wiki/ZA0018-Separate-multi-line-initializer-members);
  [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
