# ZA0051 - Keep the two units of a container together

**Suggestion, off by default.**
Removes the blank line between the only two [units](https://github.com/Nickelony/Stanza/wiki/Terms) of a container when each spans one line.

```csharp
// Before
private static string Describe(object data)
{
	string result = ResultGiver.GetResult(data);

	return result;
}

// After
private static string Describe(object data)
{
	string result = ResultGiver.GetResult(data);
	return result;
}
```

- With only two units, neither has a neighbor outside the pair to be separated from,
  so the [grouping rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#gaps-and-owners) ask for no blank line there and the one present is padding.
- A container is a block, a switch section, a type, an enum, a namespace or a file.
- A comment or a directive between the two units keeps the blank line.
  Where this rule is on, [`ZA0012`](https://github.com/Nickelony/Stanza/wiki/ZA0012-Separate-a-commented-sibling) adds none above that comment.
- The rule skips a container that holds a local function or an import directive, because it judges only two statements or members.
- The rule skips a container with a conditional directive (`#if`, `#elif`, `#else` or `#endif`)
  between its units or splitting them from the rest of the container,
  because the count then depends on the [view](https://github.com/Nickelony/Stanza/wiki/Terms).
- The grouping rules own the gap of that container in every view.
- A directive inside a unit, a `#region` and a directive outside the container's braces change no count, so the container stays judged.
- A conditional with no `#else` or `#elif` that holds every unit leaves the container judged,
  because the two units exist in the views that take it and in no other.
- In a switch section, a conditional above the next section that holds whole sections changes none of the section's statements,
  and neither does one among the `case` labels that holds only labels behind a label that stays.
