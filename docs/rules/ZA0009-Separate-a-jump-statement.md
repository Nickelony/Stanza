# ZA0009 - Separate a jump statement

**Suggestion, off by default.**
Adds a blank line above a `return`, `break`, `continue`, `throw`, `goto` or `yield break` statement
when the [stanza](https://github.com/Nickelony/Stanza/wiki/Terms) directly above it spans two or more lines.

```csharp
// Before
var order = Load(id);
Validate(order);
order.Close();
return order;

// After
var order = Load(id);
Validate(order);
order.Close();

return order;
```

One line above keeps the jump tight:

```csharp
// Unchanged
var order = Load(id);
return order;
```

- The stanza is the statements directly above the jump that no blank line separates.
  It spans from the first line of its first statement to the last line of its last statement.
- A statement that spans several lines is a stanza of several lines, so a jump below a multi-line statement fires.
  Two statements on one line span one line.
- A jump that shares a line with the statement above it leaves that statement in the stanza,
  so `x = 1;` over `x = 2; return x;` is reported, and the fix moves the jump to a line of its own below a blank line.
- A comment line between the statements counts as a line of the stanza.
  A comment above the first statement, and a comment that trails the last one, do not.
- A comment directly above the jump moves with it, so the blank line goes above the comment.
- The rule judges the statements of a block, of a switch section and at the top level of a file.
  A jump that opens its block, an `if (done) return;` and a labeled jump are left alone.
- The `break` that closes a switch section is not judged, because it ends the section rather than the logic above it.
  That is a `break` that stands in the section, or ends the block that is the section's only statement.
- A `break` anywhere else, and every other jump at the end of a section, is judged.
- A directive in the gap keeps it as written, and a directive between the statements ends the stanza,
  so every [view](https://github.com/Nickelony/Stanza/wiki/Terms) reads the same stanza.
- A gap that another rule asks for ends the stanza, and this rule leaves that gap to the rule that asks.
- Those rules are
  the [grouping rules](https://github.com/Nickelony/Stanza/wiki/Terms) around a run,
  [`ZA0001`](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-multi-line-constructs) beside a multi-line statement,
  [`ZA0015`](https://github.com/Nickelony/Stanza/wiki/ZA0015-Separate-the-import-section) below the imports,
  [`ZA0401`](https://github.com/Nickelony/Stanza/wiki/ZA0401-Separate-the-region-marker-from-its-neighbors) beside a region marker,
  [`ZA0402`](https://github.com/Nickelony/Stanza/wiki/ZA0402-Separate-the-conditional-block-from-its-neighbors) beside a conditional block and
  [`ZA0012`](https://github.com/Nickelony/Stanza/wiki/ZA0012-Separate-a-commented-sibling) above a comment.
- [`ZA0010`](https://github.com/Nickelony/Stanza/wiki/ZA0010-Separate-the-base-call) asks for the gaps beside the base call that opens or closes a member body,
  and owns the gap below an opening call even when this rule asks for it above a jump.
- A `return base.Load();` is a jump, so this rule judges the gap above it.
- A rule that is [off](https://github.com/Nickelony/Stanza/wiki/Terms) at a gap hands it back, so this rule covers the gap, a multi-line statement above it included.
- [`ZA0011`](https://github.com/Nickelony/Stanza/wiki/ZA0011-Separate-the-guard-clauses) asks for a blank line below the guards that open a block, and that gap ends the stanza where the rule is on.
- A guard counts as a line of the stanza above a jump, so `if (a) return;`, `if (b) return;` and `return c;`
  gets its blank line from this rule, and `ZA0011` leaves the gap.
- [`ZA0052`](https://github.com/Nickelony/Stanza/wiki/ZA0052-Gather-a-run-of-loose-statements) judges the gap above a jump as gathered,
  so it keeps that gap blank where gathering the run above would leave this rule asking for it.
- This rule has no tight counterpart; leave it disabled to keep jumps tight.
