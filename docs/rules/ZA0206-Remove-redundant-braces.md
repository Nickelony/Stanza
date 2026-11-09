# ZA0206 - Remove redundant braces

**Suggestion, off by default.**
Removes the braces of a chain whose branches are all single-line, the opposite of `ZA0205`,
and of any block that can lose them where `csharp_prefer_braces` is `false`.

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
	return;
else
	Skip();
```

- Loop, `using`, `lock` and `fixed` bodies are judged the same way, one body at a time.
  In a stack of `using`, `lock` or `fixed` statements, which [`ZA0205`](https://github.com/Nickelony/Stanza/wiki/ZA0205-Use-braces-consistently) leaves unbraced, only the innermost body loses its braces.
- A `try`, `catch` or `finally` body is not judged, as for [`ZA0205`](https://github.com/Nickelony/Stanza/wiki/ZA0205-Use-braces-consistently).
- Braces stay when removing them would let an `else` attach to an inner `if`,
  but a braced loop or block around that `if` closes it first, so the braces outside the loop can come off.
- A block is judged on the code as it stands, though a fix-all can remove any set of blocks.
  So a block keeps its braces when its statement could take an `else` and one follows it,
  directly or after the closing braces of enclosing blocks that could lose their own.
- A chain keeps its braces when its `else` holds only an `if`, which would read as the next link of the chain without them.
- The fix takes the blank lines between a closing brace and the `else` or `while` after it along with the brace, unless a directive stands in the gap.
  A comment in the gap stays, and the blank lines above it go.
- The rule reports nothing when `csharp_prefer_braces` is `true`.
- Braces come off only from a block whose `{` and `}` each stand alone on a line,
  around exactly one statement that fills the lines between them and is indented deeper than they are.
- A block keeps its braces when its one statement is a labeled statement, a local declaration,
  a local function or an empty statement, or when it contains a directive.
- A block with a comment or directive on a line that holds no statement is a multi-line branch,
  so its chain keeps every brace unless `csharp_prefer_braces` is `false`, as [`ZA0205`](https://github.com/Nickelony/Stanza/wiki/ZA0205-Use-braces-consistently) lists.
  A comment wholly on the statement's own line does not count, and the braces come off around it.
- A chain keeps every brace while any of its blocks must keep its own, because `IDE0011` braces a chain with one braced branch,
  unless `csharp_prefer_braces` is `false`.
- A chain keeps every brace while a conditional directive stands among its branches, unless `csharp_prefer_braces` is `false`.
  Its branches can then differ by [view](https://github.com/Nickelony/Stanza/wiki/Terms), and `ZA0205` braces the view with a multi-line branch.
- A chain keeps every brace while an `else` in disabled text can join it in another view, unless `csharp_prefer_braces` is `false`.
  That `else` follows the chain, directly or after a conditional region, or ends the disabled text above the chain.
- A conditional statement that stands alone above or below the chain changes no branch.
  Neither does a directive in the `while` of a `do` loop, nor an `else` under a condition no view meets, such as `#if false`.
- Under `csharp_prefer_braces` `false`, a chain loses each brace it can, however many of its branches are multi-line.
  A chain whose `else` holds only an `if` still keeps its braces.
