; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|----------------------------------------------------------
ZA0001  | Formatting | Warning  | Separate declaration and invocation runs.
ZA0002  | Formatting | Warning  | Separate member groups.
ZA0003  | Formatting | Disabled | Separate modifier groups.
ZA0004  | Formatting | Warning  | Separate same-type declaration groups.
ZA0005  | Formatting | Warning  | Separate multi-line constructs.
ZA0006  | Formatting | Disabled | Keep the two units of a container together.
ZA0007  | Formatting | Warning  | Separate the import section.
ZA0008  | Formatting | Disabled | Separate the import directive groups.
ZA0009  | Formatting | Warning  | Separate a file-scoped namespace declaration from its first member.
ZA0010  | Formatting | Disabled | Separate multi-line accessors.
ZA0011  | Formatting | Disabled | Keep multi-line accessors together.
ZA0012  | Formatting | Disabled | Keep single-line accessors together.
ZA0013  | Formatting | Warning  | Separate multi-line switch sections.
ZA0014  | Formatting | Disabled | Keep multi-line switch sections together.
ZA0015  | Formatting | Disabled | Keep single-line switch sections together.
ZA0016  | Formatting | Warning  | Separate multi-line switch expression arms.
ZA0017  | Formatting | Disabled | Keep multi-line switch expression arms together.
ZA0018  | Formatting | Disabled | Keep single-line switch expression arms together.
ZA0019  | Formatting | Disabled | Separate multi-line initializer members.
ZA0020  | Formatting | Disabled | Keep multi-line initializer members together.
ZA0021  | Formatting | Disabled | Keep single-line initializer members together.
ZA0022  | Formatting | Disabled | Separate multi-line collection elements.
ZA0023  | Formatting | Disabled | Keep multi-line collection elements together.
ZA0024  | Formatting | Disabled | Keep single-line collection elements together.
ZA0025  | Formatting | Disabled | Separate multi-line arguments.
ZA0026  | Formatting | Disabled | Keep multi-line arguments together.
ZA0027  | Formatting | Disabled | Keep single-line arguments together.
ZA0028  | Formatting | Disabled | Separate multi-line parameters.
ZA0029  | Formatting | Disabled | Keep multi-line parameters together.
ZA0030  | Formatting | Disabled | Keep single-line parameters together.
ZA0031  | Formatting | Disabled | Separate multi-line declarators.
ZA0032  | Formatting | Disabled | Keep multi-line declarators together.
ZA0033  | Formatting | Disabled | Keep single-line declarators together.
ZA0034  | Formatting | Disabled | Separate a clause keyword from the brace before it.
ZA0035  | Formatting | Info     | Keep clause keywords adjacent.
ZA0036  | Formatting | Info     | Remove brace padding.
ZA0037  | Formatting | Info     | Collapse empty blocks.
ZA0038  | Formatting | Info     | Use braces consistently.
ZA0039  | Formatting | Disabled | Remove redundant braces.
ZA0040  | Formatting | Disabled | Use a block body for a multi-line member.
ZA0041  | Formatting | Disabled | Use an expression body for a single-line member.
ZA0042  | Formatting | Disabled | Use a block body for a multi-line value member.
ZA0043  | Formatting | Disabled | Use an expression body for a single-line value member.
ZA0044  | Formatting | Info     | Break the expression off the signature line.
ZA0045  | Formatting | Disabled | Keep the arrow on the signature line.
ZA0046  | Formatting | Disabled | Put the arrow on the line after the signature.
ZA0047  | Formatting | Info     | Separate the region marker from its neighbors.
ZA0048  | Formatting | Disabled | Name the closing region directive.
ZA0049  | Formatting | Disabled | Remove empty regions.
ZA0050  | Formatting | Disabled | Name the arguments of a call.
ZA0051  | Formatting | Disabled | Remove a redundant argument name.
ZA0101  | Formatting | Disabled | Separate multi-line markup elements.
ZA0102  | Formatting | Disabled | Keep multi-line markup elements together.
ZA0103  | Formatting | Disabled | Keep single-line markup elements together.
ZA0104  | Formatting | Disabled | Collapse extra blank lines between markup elements.
ZA0105  | Formatting | Disabled | Separate a markup comment marker from its text.
ZA0201  | Formatting | Disabled | Lay out a documentation comment.
ZA0202  | Formatting | Disabled | Mark documentation paragraphs.
ZA0203  | Formatting | Disabled | Remove documentation padding.
ZA0204  | Formatting | Disabled | Lay out the items of a documentation list.
ZA0205  | Formatting | Disabled | Keep a documentation comment with its declaration.
ZA0206  | Formatting | Disabled | Collapse the whitespace inside a tag.
ZA0207  | Formatting | Disabled | Separate the slash of a self-closing tag.
ZA0208  | Formatting | Disabled | Keep the slash of a self-closing tag tight.
ZA0209  | Formatting | Disabled | Write an empty element as a self-closing tag.
ZA0210  | Formatting | Disabled | End the documentation text with a period.
ZA0211  | Formatting | Disabled | Mark the null keyword.
ZA0212  | Formatting | Disabled | Order the elements of a documentation comment.
ZA0213  | Formatting | Disabled | Order the items of a documentation list.
ZA0214  | Formatting | Disabled | Separate a single-line comment marker from its text.
ZA0215  | Formatting | Disabled | Separate a delimited comment marker from its text.
ZA0216  | Formatting | Disabled | Separate a trailing comment from the code.
