# ZA1006 - Break up a long else-if chain

**Suggestion, off by default.**
Reports an `if` chain that holds more branches than
[`stanza_max_else_if_branches`](https://github.com/Nickelony/Stanza/wiki/stanza_max_else_if_branches) allows.
It is a [report-only rule](https://github.com/Nickelony/Stanza/wiki/Terms), so it has no code fix.

```csharp
// Before - five branches, so a reader tests each condition in turn
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

// After - rewritten by hand, with one line for each case
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

- A branch is the `if` or an `else if` with its condition, and the closing `else` is not a branch.
- The rule reports once for a chain, at the `if` keyword that opens it.
- An `if` inside a branch, or inside an `else` block, starts a chain of its own.
- The message does not promise a `switch`, because many chains compare different operands.
  Where the conditions share no operand, a lookup table or a method for each branch may fit instead.
- Each of these reshapes the statement, so the rule only reports.
