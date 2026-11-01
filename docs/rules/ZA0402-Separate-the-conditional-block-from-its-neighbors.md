# ZA0402 - Separate the conditional block from its neighbors

**Suggestion, on by default.**
Writes a blank line above an `#if` directive and below its `#endif`,
except beside a brace of the block that holds it, or another directive.

```csharp
// Before
class C
{
	private int _total;
	#if TRACE
	private int _trace;
	#endif
	private int _sum;
}

// After
class C
{
	private int _total;

	#if TRACE
	private int _trace;
	#endif

	private int _sum;
}
```

- Only the outside of the block is judged: the branches, and the lines beside `#elif` and `#else`, stay as written.
- The directives must stand between members, statements or `using` directives.
  An `#if` inside an argument list, a parameter list, a collection or an initializer keeps its gap.
- Each [view](https://github.com/Nickelony/Stanza/wiki/Terms) judges a block by its own text.
  The token after each directive must start a unit, end a container or start a switch section.
- Every branch of the block, whether active code or disabled text, must hold whole units.
  A branch balances its braces, parentheses and brackets, and ends a statement, a member or an enum member.
- A branch does not begin with an `else`, a `catch` or a `finally`.
- The last member of an enum needs no comma, so a branch before the enum's closing brace may end in its name or its value.
- So a block that holds a clause, an attribute list or a statement head keeps its gap in every view, such as a `for (;;)` above a block.
  So does one that opens a brace in an `#if` and closes it in another.
- A block that holds the end of a switch section and the start of the next keeps its gap in every view.
- A branch is read with the blocks nested in it, so an outer block that holds the two halves of a statement in two inner blocks is separated.
- A block directly below another block starts at a unit boundary only if that block does,
  so two blocks that together complete a statement head keep their gaps.
- An `#if` before a comma-first enum member, such as `, Two`, gets a blank line above it in every view.
- The label of the next section ends a block as a closing brace does, so the block gets a blank line below it.
- Only a directive in active code is judged, so a nested `#if` in disabled text is left alone.
  A blank line there would sit in text no build reads.
- This rule shares with [`ZA0401`](https://github.com/Nickelony/Stanza/wiki/ZA0401-Separate-the-region-marker-from-its-neighbors) its exceptions for a brace, a switch label, another directive and the edge of a file,
  and its rule that a comment directly above the directive moves with it.
- The grouping rules end a run at the directive where this rule is enabled, as [the gap rules](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-comment-or-a-directive-in-a-gap) say.
- This rule has no tight counterpart; leave it disabled to keep conditional blocks tight.
