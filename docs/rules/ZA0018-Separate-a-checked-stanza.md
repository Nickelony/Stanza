# ZA0018 - Separate a checked stanza

**Suggestion, off by default.**
Adds blank lines around a guard chain and the bindings directly above it that its checks read.

```csharp
// Before
Audit(id);
var customer = repository.Find(id);
if (customer is null) return;
Show(customer);

// After
Audit(id);

var customer = repository.Find(id);
if (customer is null) return;

Show(customer);
```

- A checked stanza holds one or more guards and the contiguous bindings above them whose names the checks read.
  A binding the checks do not read stays above the stanza; a `nameof` operand does not attach it.
- A stanza that opens or ends its block needs no blank line at that edge.
- A stanza made only of narrowing assertions gets a blank line above it, and this rule asks for none below it.
  A plain assertion that does not narrow a name is no guard for this rule.
- A neighboring run that mixes narrowing and ordinary assertions is an assertion phase, so this rule leaves it alone.
- A comment or directive between a binding and a check ends the binding group.
- The rule judges blocks, switch sections and top-level statements.
- [`ZA0011`](https://github.com/Nickelony/Stanza/wiki/ZA0011-Separate-the-guard-clauses) owns the gap below an opening guard chain where it is on.
- [`ZA0015`](https://github.com/Nickelony/Stanza/wiki/ZA0015-Separate-the-import-section) owns the gap below the last import,
  so a top-level stanza that follows the imports gets that blank line from it alone.
- [`ZA0009`](https://github.com/Nickelony/Stanza/wiki/ZA0009-Separate-a-jump-statement) owns the lower gap when it asks for a blank line above a jump.
- Where [`ZA0019`](https://github.com/Nickelony/Stanza/wiki/ZA0019-Separate-receiver-groups) asks for the same gap, this rule owns it.
- The keep rules and the rules that group runs own their gaps first, as [A keep rule and the other owners of its gap](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-keep-rule-and-the-other-owners-of-its-gap) says.
- Where [`ZA0064`](https://github.com/Nickelony/Stanza/wiki/ZA0064-Keep-dependent-bindings-together) keeps the checked binding with the bindings it depends on,
  the upper blank line goes above that whole kept group, as [The boundary of a kept group](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#the-boundary-of-a-kept-group) says.
