# Relationship rationale

The relationship rules join or separate statements only where their syntax identifies one step.
These layouts were weighed and left out, and each row says why.

| Layout | Decision | Reason |
| --- | --- | --- |
| Keep all single-line `if` statements together | Left to the statement run rules | Guards have rules of their own, and an ordinary conditional is a statement like any other. |
| Join every call on a shared receiver | Only the receivers `receiver-roots` selects form groups | A shared logger, mock or `self` does not identify one operation. |
| Join every binding to the next statement that reads it | Only chains of plain-name bindings, each reading the one above | Nearly every statement reads the one above it, so such a rule would join most of a function. |
| Keep an assert with the `with ... as name` block above it | Left apart | The block is the act phase of a test, and the assertion opens its assert phase. |
| Separate every lone assert from the next statement | Left to the assertion runs | A test assertion follows the assertion run rules, and a narrowing assertion belongs to the flow. |
| Group multi-line declarations by their callee | No grouping by callee | A shared callee does not bound the size of a group of long calls. |
| Leave dispatch chains out of guard grouping | Ordinary guard recognition | Tight dispatch guards read as cases, and they stay guards. |
