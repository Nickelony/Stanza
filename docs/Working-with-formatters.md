# Working with formatters

The rules edit lines, so whichever tool writes the file last decides its layout.

## Run order

In a project that uses the packages and CSharpier, run the tools in this order:

```text
dotnet format analyzers --severity info --exclude-diagnostics ZA0301 ZA0302 ZA0303 ZA0304 ZA0305 ZA0306 ZA0307
dotnet format style --severity info --exclude-diagnostics IDE0130
dotnet csharpier format .
dotnet format analyzers --severity info --diagnostics ZA0301 ZA0302 ZA0303 ZA0304 ZA0305 ZA0306 ZA0307
dotnet csharpier format .
dotnet format analyzers --severity info --exclude-diagnostics ZA0301 ZA0302 ZA0303 ZA0304 ZA0305 ZA0306 ZA0307
```

[`stanza-format`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Format/README.md) runs these commands in turn,
and its [command reference](https://github.com/Nickelony/Stanza/wiki/Format-command-reference#layout) names the oldest CSharpier they need.

- The first command applies every fix but the body shapes, and the second the IDE style fixes, so CSharpier lays out the code they write.
- The second leaves out `IDE0130`, because its fix moves a file to the directory its namespace names,
  and `dotnet format` stops with `Changing document properties is not supported` on such a fix.
- The body-shape rules, [`ZA0301`](https://github.com/Nickelony/Stanza/wiki/ZA0301-Use-a-block-body-for-a-multi-line-member) to `ZA0307`,
  judge how many lines a member spans, so they run on what CSharpier wrote.
  A body-shape rule that runs before it judges a layout about to change, so a second pass flips the member back.
- CSharpier runs again because a block body that ZA0301 writes keeps the expression's continuation lines where they were.
- The last command runs the fixes again, which put back the blank lines CSharpier drops.

Leave out the whitespace pass of `dotnet format`, as `dotnet format analyzers` does,
because it lays out some code differently from CSharpier, such as a property whose type spans lines.

CSharpier is optional.
`stanza-format --layout whitespace` runs that pass in CSharpier's place, and `--layout none` runs the rule passes alone;
the [`--layout` section](https://github.com/Nickelony/Stanza/wiki/Format-command-reference#layout) says which one a run uses by default.

A rule that disagrees with CSharpier falls into one of two groups:

- Most write a blank line that CSharpier removes.
  `stanza-format` writes the line back in its last step and its check ignores it, so the `strong` level keeps those rules,
  and the `csharpier` value of [`StanzaFormatter`](https://github.com/Nickelony/Stanza/wiki/Presets#which-formatter-value) disables them for a team that also runs CSharpier alone.
- A few can never settle, because CSharpier writes back what they change or they have no fix, so no level enables them.

[ZA0305](#arrow-expressions) disagrees only when the widths differ, as its section below says.

## Layout that CSharpier undoes

- **Lists** (collection elements, arguments, parameters, tuple elements, declarators):
  CSharpier re-prints the list and drops its blank lines,
  so a [separating rule](https://github.com/Nickelony/Stanza/wiki/Terms) for a list disagrees with it.
- **The accessor [gap](https://github.com/Nickelony/Stanza/wiki/Terms)**: CSharpier drops the blank line between accessors whatever their shape,
  so the separating accessor rule disagrees with it.
- **A commented sibling in a list** ([`ZA0012`](https://github.com/Nickelony/Stanza/wiki/ZA0012-Separate-a-commented-sibling)):
  CSharpier drops the blank line the rule writes above a comment in an argument, parameter, tuple, declarator, array initializer or accessor list.
  It keeps the line among statements, members, enum members, object initializers, switch arms and switch sections.
- **Before a clause keyword**: CSharpier drops the blank line before a keyword such as `else`,
  so the separating rule for clause keywords disagrees with it.
- **The trailing comma** ([`ZA0199`](https://github.com/Nickelony/Stanza/wiki/ZA0199-Remove-the-trailing-comma)):
  CSharpier writes back the comma the rule removes, as [the entry for `ZA0198`](#layout-that-agrees-with-csharpier) says,
  so the rule and the formatter undo each other.
- **The slash of a self-closing markup element** ([`ZA2009`](https://github.com/Nickelony/Stanza/wiki/ZA2009-Keep-the-slash-of-a-self-closing-markup-element-tight)):
  CSharpier writes `<Compile Include="a.cs" />` when it formats a project file, so the tight rule disagrees with it.
- **A line past the width** ([`ZA1001`](https://github.com/Nickelony/Stanza/wiki/ZA1001-Shorten-a-long-line)):
  CSharpier wraps code to its own print width, so the report-only rule would measure a width the formatter owns.

The rule [`ZA0141`](https://github.com/Nickelony/Stanza/wiki/ZA0141-Separate-multi-line-collection-elements) adds the blank line shown in the After part of the example.
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
- **Switch sections, switch expression arms, object initializer members, region markers, conditional blocks and jump statements**:
  CSharpier keeps the blank lines the separating rules write.
- **Brace padding and empty blocks**: CSharpier already writes what these rules ask for.
- **The trailing comma** ([`ZA0198`](https://github.com/Nickelony/Stanza/wiki/ZA0198-Add-a-trailing-comma)):
  CSharpier writes the comma after the last element of every list it breaks across lines, apart from a property pattern, which it keeps as written,
  and drops it from a list it joins on one line.
- **The closing conditional directive** ([`ZA0404`](https://github.com/Nickelony/Stanza/wiki/ZA0404-Name-the-closing-conditional-directive)):
  CSharpier leaves the comment after an `#endif` as written.
- **Extra blank lines** ([`ZA0056`](https://github.com/Nickelony/Stanza/wiki/ZA0056-Collapse-extra-blank-lines)):
  CSharpier cuts each run to one blank line, so the rule agrees with it at the default cap and disagrees at a cap above `1`.
- **Documentation comments**: CSharpier leaves them as written.
- **Markup element padding and the spaced slash** ([`ZA2005`](https://github.com/Nickelony/Stanza/wiki/ZA2005-Remove-markup-element-padding), [`ZA2008`](https://github.com/Nickelony/Stanza/wiki/ZA2008-Separate-the-slash-of-a-self-closing-markup-element)):
  CSharpier removes the blank lines beside the tags of an element and writes the space before a slash when it formats a project file.

## Fixes that open work

A fix that widens a line can hand work to a width rule.
Naming the arguments of a call with [`ZA0501`](https://github.com/Nickelony/Stanza/wiki/ZA0501-Name-the-arguments-of-a-call) can push an expression body past the width that [`ZA0305`](https://github.com/Nickelony/Stanza/wiki/ZA0305-Break-the-expression-off-the-signature-line) measures.
Writing an empty element as a self-closing tag with [`ZA0909`](https://github.com/Nickelony/Stanza/wiki/ZA0909-Write-an-empty-element-as-a-self-closing-tag) shortens its line,
so [`ZA0901`](https://github.com/Nickelony/Stanza/wiki/ZA0901-Lay-out-a-documentation-comment) can wrap the comment again.

Where a run crosses one of the gaps that two grouping rules ask for on either side of a unit, [the lower gap waits for the upper one](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#one-run-of-statements-several-owners).

A second [pass](https://github.com/Nickelony/Stanza/wiki/Terms) settles what the first one leaves.

## Arrow expressions

[ZA0305](https://github.com/Nickelony/Stanza/wiki/ZA0305-Break-the-expression-off-the-signature-line) is the only arrow rule in the default level.
When [`stanza_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_max_line_length) is narrower than the formatter's print width,
the formatter rejoins the lines ZA0305 breaks, so disable ZA0305 in that case.

## Documentation XML

[`stanza_markup_doc_reprint`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_doc_reprint) lays out a member body as text,
but CSharpier lays out a body line that starts with an element as a child element, so the two never agree on that line.
List each file the [reprint](https://github.com/Nickelony/Stanza/wiki/Terms) rewrites in `.csharpierignore`, so `stanza-markup` alone lays it out.

## IDE style rules

The built-in IDE style rules write code too:

- `csharp_style_expression_bodied_*` set to `true` prefers expression bodies,
  the opposite of ZA0301 and ZA0303 for a multi-line member.
- `csharp_prefer_braces = true` makes ZA0206 report nothing,
  and `csharp_prefer_braces = false` makes ZA0205 report nothing.
- `csharp_prefer_braces = when_multiline`, or no value, leaves both ZA0205 and ZA0206 reporting.
- `csharp_preserve_single_line_blocks = false` makes ZA0204 report nothing.
- `csharp_style_allow_embedded_statements_on_same_line_experimental = false` makes `IDE2001` move an empty block body below its header,
  but CSharpier writes it on the header's line, as in `for (...) { }`.
  Under CSharpier, write the body as `;` on its own line or as a block that holds a comment, which both keep.
- `IDE0045` and `IDE0046` ask for the opposite of [ZA1007](https://github.com/Nickelony/Stanza/wiki/ZA1007-Break-up-a-long-ternary-chain),
  and that page says how to silence them.
