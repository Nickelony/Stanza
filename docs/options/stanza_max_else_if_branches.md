# stanza_max_else_if_branches

Controls how many branches an `if` chain may hold before [`ZA1006`](https://github.com/Nickelony/Stanza/wiki/ZA1006-Break-up-a-long-else-if-chain) reports it.
Default: `4`.
Read by [`ZA1006`](https://github.com/Nickelony/Stanza/wiki/ZA1006-Break-up-a-long-else-if-chain), whose page shows a chain above the default limit.

| Value                                           | Effect                                                    |
| ----------------------------------------------- | --------------------------------------------------------- |
| `0`                                             | the rule reports no chain                                 |
| a number from 1 to 1000                         | a chain is reported when it holds more branches than that |
| below zero, above 1000, absent, or not a number | the default                                               |

At the default, four branches pass and a fifth is reported:

```csharp
// Unchanged - four branches
if (extension == ".cs")
{
	return "C#";
}
else if (extension == ".fs")
{
	return "F#";
}
else if (extension == ".vb")
{
	return "Visual Basic";
}
else if (extension == ".md")
{
	return "Markdown";
}

// Before - five branches
if (extension == ".cs")
{
	return "C#";
}
else if (extension == ".fs")
{
	return "F#";
}
else if (extension == ".vb")
{
	return "Visual Basic";
}
else if (extension == ".md")
{
	return "Markdown";
}
else if (extension == ".json")
{
	return "JSON";
}

// After
return extension switch
{
	".cs" => "C#",
	".fs" => "F#",
	".vb" => "Visual Basic",
	".md" => "Markdown",
	".json" => "JSON",
	_ => "unknown",
};
```

At a limit of `1`, two branches are reported:

```csharp
// Before at a limit of 1 - two branches
if (count == 0)
{
	return "none";
}
else if (count == 1)
{
	return "one";
}

// After at a limit of 1
if (count == 0)
{
	return "none";
}

return count == 1 ? "one" : "many";
```

- The count covers the branches of one chain: an `if` that starts a chain of its own inside a branch is counted by itself.
- The closing `else` is not a branch.
