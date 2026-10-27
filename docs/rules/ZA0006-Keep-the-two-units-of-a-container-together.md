# ZA0006 - Keep the two units of a container together

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
- The rule skips a container that holds a local function or an import directive, because it judges only two statements or members.
