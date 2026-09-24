# ZA0033 - Separate a clause keyword from the brace before it

**Suggestion, off by default.**
Adds a blank line between the closing brace of a branch and the clause keyword that continues the statement,
when that keyword already stands on its own line.

```csharp
// Before
if (options.ValidateOnLoad)
{
	Validate(options);
}
else
{
	options.ValidateOnLoad = true;
}

// After
if (options.ValidateOnLoad)
{
	Validate(options);
}

else
{
	options.ValidateOnLoad = true;
}
```

- The clause keywords are `else`, `catch` and `finally` after a braced branch, and the `while` that closes a `do` loop with a braced body.
- A keyword on the brace's line, as in `} else {`, has no [gap](https://github.com/Nickelony/Stanza/wiki/Terms) to separate,
  so the rule reports nothing.
- A comment or directive in the gap follows [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap).
- The [tight rule](https://github.com/Nickelony/Stanza/wiki/Terms) is [`ZA0034`](https://github.com/Nickelony/Stanza/wiki/ZA0034-Keep-clause-keywords-adjacent);
  [the pair table](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#pairs-and-their-default-halves) names the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
- [Working with formatters](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#layout-that-csharpier-undoes) says why the rule is off by default.
