# ZA1007 - Break up a long ternary chain

**Suggestion, off by default.**
Reports a ternary chain that holds more `?` operators than
[`stanza_max_ternary_operators`](https://github.com/Nickelony/Stanza/wiki/stanza_max_ternary_operators) allows.
It is a [report-only rule](https://github.com/Nickelony/Stanza/wiki/Terms), so it has no code fix.

```csharp
// Before - three operators, so a reader weighs each condition against the ones above it
var label = value > 2 ? "high" : value > 1 ? "medium" : value > 0 ? "low" : "none";

// After - rewritten by hand, with one line for each case
var label = value switch
{
	> 2 => "high",
	> 1 => "medium",
	> 0 => "low",
	_ => "none",
};
```

- The rule reports once for a chain, at its outermost ternary, so a chain of five operators gives one diagnostic.
- A ternary nested in a branch or in the condition counts, also inside parentheses and arguments.
- The operators `??` and `?.` are not ternaries and do not count.
- A lambda or an anonymous method starts a chain of its own, so its ternaries do not add to the chain around it.
- The message suggests an `if` statement or a switch expression.
  Writing either one means choosing between them and reshaping the statement around the expression, so the rule only reports.
- The IDE style rules `IDE0045` and `IDE0046` ask for the opposite.
  They turn the last `if` and `else` pair of an `if` chain that assigns or returns into a conditional.
- Repeating the fix of those two rules folds the whole `if` chain back, but a switch expression raises neither one.
  Setting `dotnet_style_prefer_conditional_expression_over_assignment` and
  `dotnet_style_prefer_conditional_expression_over_return` to `false` silences both.
