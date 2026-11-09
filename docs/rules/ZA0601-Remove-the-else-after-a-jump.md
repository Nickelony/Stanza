# ZA0601 - Remove the else after a jump

**Suggestion, off by default.**
Moves the statements of an `else` after their `if`,
when the branch of the `if` ends in a `return`, `throw`, `break`, `continue`, `goto` or `yield break`.
The branch never reaches the `else`, so the statements run on the same paths and stand one level less deep.

```csharp
// Before
if (root is null)
{
	return;
}
else
{
	Skip();
}

// After
if (root is null)
{
	return;
}

Skip();
```

An `else if` and a branch that can fall through keep the `else`:

```csharp
// Unchanged
if (root is null)
{
	return;
}
else if (root.IsEmpty)
{
	Skip();
}
```

- The branch ends in a jump when its last statement is one, whether the branch is a block or a single statement.
- The rule leaves an `else if`, since the statements of the last `else` would run for the earlier branches too.
- The `if` has to be a statement of a block, so the statements land among their siblings.
  Any other `if` keeps its `else`, such as an `else if`, the unbraced body of a loop or another statement,
  a labeled statement, a statement of a switch section or a top-level statement.
- An empty `else` stays.
- The statements join the block around the chain, so the `else` stays when that block reads a name they declare.
  Those names are a local, a local function, a label, and a variable that an `out` argument, a pattern or a deconstruction declares.
- Disabled text that holds such a name counts as a read, because another view reads it as code.
- A name that the condition of the `if` declares is in that block already, so it is no obstacle.
- A `using` declaration, also one behind a label, stays in its `else`, as it would be disposed later in the block around.
- An `if` inside the `else` of another `if` that this rule reports waits for the outer one, so a Fix All never moves a name twice.
- A directive in the statement, above it, or in the trivia of the token after it keeps the `else` in every
  [view](https://github.com/Nickelony/Stanza/wiki/Terms), because another view can join the `else` to another `if`.
- A comment beside the `else` and its braces stays on a line of its own, or beside the branch when it began there.
- A token after the closing brace on its line goes to a line of its own when a line comment now ends the line before it.
- The blank lines between the branch and the first statement, and between the last statement and the closing brace, go with the `else`.
- The statements come out by one indent step when every line of them is indented by it and no string spans lines.
  The lines after the first of a block comment beside them come out by the same step.
- Otherwise they keep their indentation, and the layout step of [`stanza-format`](https://github.com/Nickelony/Stanza/wiki/Format-command-reference#steps) settles it.
- The fix writes the blank lines [`ZA0001`](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-multi-line-constructs) asks for around the chain and the statements it frees,
  as [A blank line a fix leaves behind](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-blank-line-a-fix-leaves-behind) says.
- The rule waits while [`ZA0205`](https://github.com/Nickelony/Stanza/wiki/ZA0205-Use-braces-consistently) has braces to add to the chain,
  so the two reach the same text in either order.
- [`ZA0206`](https://github.com/Nickelony/Stanza/wiki/ZA0206-Remove-redundant-braces) leaves the same text whether it runs before or after.
- The blank line that [`ZA0201`](https://github.com/Nickelony/Stanza/wiki/ZA0201-Separate-a-clause-keyword-from-the-brace-before-it) and
  [`ZA0202`](https://github.com/Nickelony/Stanza/wiki/ZA0202-Keep-clause-keywords-adjacent) settle before an `else` goes with the `else`.
- `IDE0046` reports an `if` that returns on both paths whether or not the `else` stands, so it reports the result of this fix too.
- `IDE0045` reports only the assignment form, which has no jump to end the branch.
- Roslynator offers the same change as `RCS1211`, hidden by default.
