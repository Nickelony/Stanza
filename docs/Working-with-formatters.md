# Working with formatters

The rules edit lines, so whichever tool writes the file last decides its layout.

## Run order

In a project that uses the packages and CSharpier, run the tools in this order:

```text
dotnet format analyzers --severity info --exclude-diagnostics ZA0040 ZA0041 ZA0042 ZA0043 ZA0044 ZA0045 ZA0046
dotnet format style --severity info
dotnet csharpier format .
dotnet format analyzers --severity info --diagnostics ZA0040 ZA0041 ZA0042 ZA0043 ZA0044 ZA0045 ZA0046
dotnet csharpier format .
dotnet format analyzers --severity info --exclude-diagnostics ZA0040 ZA0041 ZA0042 ZA0043 ZA0044 ZA0045 ZA0046
```

[`stanza-format`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Format/README.md) runs these commands in turn,
and its [command reference](https://github.com/Nickelony/Stanza/wiki/Format-command-reference#layout) names the oldest CSharpier they need.

- The first command applies every fix but the body shapes, and the second the IDE style fixes, so CSharpier lays out the code they write.
- The body-shape rules, [`ZA0040`](https://github.com/Nickelony/Stanza/wiki/ZA0040-Use-a-block-body-for-a-multi-line-member) to `ZA0046`,
  judge how many lines a member spans, so they run on what CSharpier wrote.
  A body-shape rule that runs before it judges a layout about to change, so a second pass flips the member back.
- CSharpier runs again because a block body that ZA0040 writes keeps the expression's continuation lines where they were.
- The last command runs the fixes again, which put back the blank lines CSharpier drops.

Leave out the whitespace pass of `dotnet format`, as `dotnet format analyzers` does,
because it lays out some code differently from CSharpier, such as a property whose type spans lines.

CSharpier is optional.
`stanza-format --layout whitespace` runs that pass in CSharpier's place, and `--layout none` runs the rule passes alone;
the [`--layout` section](https://github.com/Nickelony/Stanza/wiki/Format-command-reference#layout) says which one a run uses by default.

A rule that disagrees with CSharpier is **off** by default, except [ZA0044](#arrow-expressions), which has its own section below.

## Layout that CSharpier undoes

- **Lists** (collection elements, arguments, parameters, declarators):
  CSharpier re-prints the list and drops its blank lines,
  so a [separating rule](https://github.com/Nickelony/Stanza/wiki/Terms) for a list disagrees with it.
- **The accessor [gap](https://github.com/Nickelony/Stanza/wiki/Terms)**: CSharpier drops the blank line between accessors whatever their shape,
  so the separating accessor rule disagrees with it.
- **Before a clause keyword**: CSharpier drops the blank line before a keyword such as `else`,
  so the separating rule for clause keywords disagrees with it.

The rule [`ZA0022`](https://github.com/Nickelony/Stanza/wiki/ZA0022-Separate-multi-line-collection-elements) adds the blank line shown in the After part of the example.
CSharpier formats both versions to the same text, so the rule and the formatter undo each other.

```csharp
// Before
var handlers = new IHandler[]
{
	new ConfigurationHandler(
		ConfigurationSectionKind.Application),
	new ValidationHandler(
		ConfigurationSectionKind.Application),
};

// After the rule
var handlers = new IHandler[]
{
	new ConfigurationHandler(
		ConfigurationSectionKind.Application),

	new ValidationHandler(
		ConfigurationSectionKind.Application),
};
```

## Layout that agrees with CSharpier

- **[Tight rules](https://github.com/Nickelony/Stanza/wiki/Terms) of a list**: they agree with CSharpier, so they can be [enabled](https://github.com/Nickelony/Stanza/wiki/Terms) without their separating rules.
- **Switch sections, switch expression arms and region markers**: CSharpier keeps the blank lines the separating rules write.
- **Brace padding and empty blocks**: CSharpier already writes what these rules ask for.
- **Documentation comments**: CSharpier leaves them as written.

## Fixes that open work

A fix that widens a line can hand work to a width rule.
Naming the arguments of a call with [`ZA0050`](https://github.com/Nickelony/Stanza/wiki/ZA0050-Name-the-arguments-of-a-call) can push an expression body past the width that [`ZA0044`](https://github.com/Nickelony/Stanza/wiki/ZA0044-Break-the-expression-off-the-signature-line) measures.
Writing an empty element as a self-closing tag with [`ZA0209`](https://github.com/Nickelony/Stanza/wiki/ZA0209-Write-an-empty-element-as-a-self-closing-tag) shortens its line,
so [`ZA0201`](https://github.com/Nickelony/Stanza/wiki/ZA0201-Lay-out-a-documentation-comment) can wrap the comment again.
A second [pass](https://github.com/Nickelony/Stanza/wiki/Terms) settles what the first one leaves.

## Arrow expressions

[ZA0044](https://github.com/Nickelony/Stanza/wiki/ZA0044-Break-the-expression-off-the-signature-line) is the only arrow rule that is on by default.
When [`stanza_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_max_line_length) is narrower than the formatter's print width,
the formatter rejoins the lines ZA0044 breaks, so disable ZA0044 in that case.

## Documentation XML

[`stanza_markup_doc_reprint`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_doc_reprint) lays out a member body as text,
but CSharpier lays out a body line that starts with an element as a child element, so the two never agree on that line.
List each file the [reprint](https://github.com/Nickelony/Stanza/wiki/Terms) rewrites in `.csharpierignore`, so `stanza-markup` alone lays it out.

## IDE style rules

The built-in IDE style rules write code too:

- `csharp_style_expression_bodied_*` set to `true` prefers expression bodies,
  the opposite of ZA0040 and ZA0042 for a multi-line member.
- `csharp_prefer_braces = true` makes ZA0039 report nothing,
  and `csharp_prefer_braces = false` makes ZA0038 report nothing.
- `csharp_prefer_braces = when_multiline`, or no value, leaves both ZA0038 and ZA0039 reporting.
- `csharp_preserve_single_line_blocks = false` makes ZA0037 report nothing.
