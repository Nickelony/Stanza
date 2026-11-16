# ZA0019 - Separate receiver groups

**Suggestion, off by default.**
Adds blank lines around groups of single-line operations through selected receivers.

With [`stanza_receiver_roots`](https://github.com/Nickelony/Stanza/wiki/stanza_receiver_roots) set to `item`
and [`stanza_receiver_comparison`](https://github.com/Nickelony/Stanza/wiki/stanza_receiver_comparison) set to `path`:

```csharp
// Before
item.Call1();
item.Call2();
item.Child.Call1();
item.Child.Call2();

// After
item.Call1();
item.Call2();

item.Child.Call1();
item.Child.Call2();
```

- A boundary needs at least one neighboring receiver group with two or more operations.
  Unrelated singleton calls do not each get a blank line.
- Direct calls, awaited calls, member assignments, compound assignments and member updates can share a group.
  An assignment belongs to the group of its owning receiver, which excludes the assigned member,
  and [`stanza_receiver_comparison`](https://github.com/Nickelony/Stanza/wiki/stanza_receiver_comparison) says how receivers compare.
- Declarations and bare local assignments are outside receiver groups. No receiver is inferred from a value or argument.
- Multi-line operations, assertions, guards, unsupported paths, comments and directives end groups.
  Consecutive operations must occupy separate physical lines.
- Blocks, switch sections and top-level statements qualify; member and list containers do not.
- The rule adds no blank line at the start or the end of a block.
  A gap that a keep rule or an earlier separating rule owns stays as that rule leaves it.
- [`ZA0018`](https://github.com/Nickelony/Stanza/wiki/ZA0018-Separate-a-checked-stanza) owns a gap both rules ask for,
  including the gap above a kept group that takes an edge from inside it, so this rule reports nothing there.
- [`ZA0063`](https://github.com/Nickelony/Stanza/wiki/ZA0063-Keep-a-receiver-group-together) removes the blank lines inside a group, and both rules can be enabled.
