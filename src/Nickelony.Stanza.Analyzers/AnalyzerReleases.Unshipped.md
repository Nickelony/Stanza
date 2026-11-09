; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|----------------------------------------------------------
ZA0001  | Formatting | Info     | Separate multi-line constructs.
ZA0002  | Formatting | Info     | Separate declaration runs.
ZA0003  | Formatting | Disabled | Separate same-type declaration runs.
ZA0004  | Formatting | Info     | Separate invocation runs.
ZA0005  | Formatting | Info     | Separate assertion runs.
ZA0006  | Formatting | Disabled | Separate await runs.
ZA0007  | Formatting | Disabled | Separate event subscription runs.
ZA0008  | Formatting | Disabled | Separate assignment runs.
ZA0009  | Formatting | Disabled | Separate a jump statement.
ZA0010  | Formatting | Disabled | Separate the base call.
ZA0011  | Formatting | Disabled | Separate the guard clauses.
ZA0012  | Formatting | Disabled | Separate a commented sibling.
ZA0013  | Formatting | Info     | Separate member groups.
ZA0014  | Formatting | Disabled | Separate modifier groups.
ZA0015  | Formatting | Info     | Separate the import section.
ZA0016  | Formatting | Disabled | Separate the import directive groups.
ZA0017  | Formatting | Info     | Separate a file-scoped namespace declaration from its first member.
ZA0051  | Formatting | Disabled | Keep the two units of a container together.
ZA0052  | Formatting | Disabled | Gather a run of loose statements.
ZA0053  | Formatting | Disabled | Gather a run of loose members.
ZA0054  | Formatting | Disabled | Keep a run of statements together.
ZA0055  | Formatting | Disabled | Keep a run of members together.
ZA0056  | Formatting | Info     | Collapse extra blank lines.
ZA0101  | Formatting | Disabled | Separate multi-line accessors.
ZA0102  | Formatting | Info     | Keep multi-line accessors together.
ZA0103  | Formatting | Info     | Keep single-line accessors together.
ZA0111  | Formatting | Info     | Separate multi-line switch sections.
ZA0112  | Formatting | Disabled | Keep multi-line switch sections together.
ZA0113  | Formatting | Info     | Keep single-line switch sections together.
ZA0121  | Formatting | Info     | Separate multi-line switch expression arms.
ZA0122  | Formatting | Disabled | Keep multi-line switch expression arms together.
ZA0123  | Formatting | Info     | Keep single-line switch expression arms together.
ZA0131  | Formatting | Disabled | Separate multi-line initializer members.
ZA0132  | Formatting | Disabled | Keep multi-line initializer members together.
ZA0133  | Formatting | Info     | Keep single-line initializer members together.
ZA0141  | Formatting | Disabled | Separate multi-line collection elements.
ZA0142  | Formatting | Disabled | Keep multi-line collection elements together.
ZA0143  | Formatting | Info     | Keep single-line collection elements together.
ZA0151  | Formatting | Disabled | Separate multi-line arguments.
ZA0152  | Formatting | Disabled | Keep multi-line arguments together.
ZA0153  | Formatting | Info     | Keep single-line arguments together.
ZA0161  | Formatting | Disabled | Separate multi-line parameters.
ZA0162  | Formatting | Disabled | Keep multi-line parameters together.
ZA0163  | Formatting | Info     | Keep single-line parameters together.
ZA0171  | Formatting | Disabled | Separate multi-line declarators.
ZA0172  | Formatting | Disabled | Keep multi-line declarators together.
ZA0173  | Formatting | Info     | Keep single-line declarators together.
ZA0198  | Formatting | Disabled | Add a trailing comma.
ZA0199  | Formatting | Disabled | Remove the trailing comma.
ZA0201  | Formatting | Disabled | Separate a clause keyword from the brace before it.
ZA0202  | Formatting | Info     | Keep clause keywords adjacent.
ZA0203  | Formatting | Info     | Remove brace padding.
ZA0204  | Formatting | Info     | Collapse empty blocks.
ZA0205  | Formatting | Info     | Use braces consistently.
ZA0206  | Formatting | Disabled | Remove redundant braces.
ZA0301  | Formatting | Info     | Use a block body for a multi-line member.
ZA0302  | Formatting | Disabled | Use an expression body for a single-line member.
ZA0303  | Formatting | Disabled | Use a block body for a multi-line value member.
ZA0304  | Formatting | Disabled | Use an expression body for a single-line value member.
ZA0305  | Formatting | Info     | Break the expression off the signature line.
ZA0306  | Formatting | Disabled | Keep the arrow on the signature line.
ZA0307  | Formatting | Disabled | Put the arrow on the line after the signature.
ZA0401  | Formatting | Info     | Separate the region marker from its neighbors.
ZA0402  | Formatting | Info     | Separate the conditional block from its neighbors.
ZA0403  | Formatting | Disabled | Name the closing region directive.
ZA0404  | Formatting | Disabled | Name the closing conditional directive.
ZA0405  | Formatting | Info     | Remove empty regions.
ZA0501  | Formatting | Disabled | Name the arguments of a call.
ZA0502  | Formatting | Disabled | Name the literal arguments of a call.
ZA0503  | Formatting | Disabled | Remove a redundant argument name.
ZA0601  | Formatting | Disabled | Remove the else after a jump.
ZA0602  | Formatting | Disabled | Merge nested if statements.
ZA0651  | Formatting | Info     | Use a pattern for a null check.
ZA0652  | Formatting | Disabled | Use an equality operator for a null check.
ZA0901  | Formatting | Disabled | Lay out a documentation comment.
ZA0902  | Formatting | Disabled | Mark documentation paragraphs.
ZA0903  | Formatting | Disabled | Remove documentation padding.
ZA0904  | Formatting | Disabled | Lay out the items of a documentation list.
ZA0905  | Formatting | Info     | Keep a documentation comment with its declaration.
ZA0906  | Formatting | Info     | Collapse the whitespace inside a tag.
ZA0907  | Formatting | Disabled | Separate the slash of a self-closing tag.
ZA0908  | Formatting | Disabled | Keep the slash of a self-closing tag tight.
ZA0909  | Formatting | Info     | Write an empty element as a self-closing tag.
ZA0910  | Formatting | Disabled | Capitalize the documentation text.
ZA0911  | Formatting | Disabled | End the documentation text with a period.
ZA0912  | Formatting | Disabled | Mark the language keywords.
ZA0913  | Formatting | Disabled | Order the elements of a documentation comment.
ZA0914  | Formatting | Disabled | Order the items of a documentation list.
ZA0951  | Formatting | Info     | Separate a single-line comment marker from its text.
ZA0952  | Formatting | Info     | Separate a delimited comment marker from its text.
ZA0953  | Formatting | Info     | Separate a trailing comment from the code.
ZA1001  | Formatting | Disabled | Shorten a long line.
ZA1002  | Formatting | Disabled | Shorten a long member.
ZA1003  | Formatting | Disabled | Shorten a long parameter list.
ZA1004  | Formatting | Disabled | Flatten deeply nested code.
ZA1005  | Formatting | Disabled | Shorten a long condition.
ZA1006  | Formatting | Disabled | Break up a long else-if chain.
ZA1007  | Formatting | Disabled | Break up a long ternary chain.
ZA2001  | Formatting | Disabled | Separate multi-line markup elements.
ZA2002  | Formatting | Disabled | Keep multi-line markup elements together.
ZA2003  | Formatting | Disabled | Keep single-line markup elements together.
ZA2004  | Formatting | Disabled | Separate a commented markup element.
ZA2005  | Formatting | Disabled | Remove markup element padding.
ZA2006  | Formatting | Disabled | Collapse extra blank lines between markup elements.
ZA2007  | Formatting | Disabled | Separate a markup comment marker from its text.
ZA2008  | Formatting | Disabled | Separate the slash of a self-closing markup element.
ZA2009  | Formatting | Disabled | Keep the slash of a self-closing markup element tight.
ZA2010  | Formatting | Disabled | Write an empty markup element as a self-closing tag.
