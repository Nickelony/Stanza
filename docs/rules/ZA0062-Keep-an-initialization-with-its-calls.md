# ZA0062 - Keep an initialization with its calls

**Suggestion, off by default.**
Removes the blank line between a single-line local initialization that opens a stanza and its first single-line call on that local.

```csharp
// Before
Print();

var item = new Item();

item.Call1();
item.Call2();

// After
Print();

var item = new Item();
item.Call1();
item.Call2();

// Unchanged - the initialization belongs to the preceding stanza
Print();
var item = new Item();

item.Call1();
item.Call2();
```

- The initialization opens its stanza: it opens the block, has a blank line above it that no run rule removes,
  or has one that a separating rule asks for.
- The declaration binds one local to a constructor or factory call, including an awaited factory call.
  Parentheses and null suppression around the initializer are transparent.
- The next statement calls a method directly on that local, optionally awaited.
- Where [`ZA0013`](https://github.com/Nickelony/Stanza/wiki/ZA0013-Separate-a-jump-statement) is on, it counts the initialization and its call as a stanza of two lines before the blank line between them is removed,
  so the jump below them gets its blank line in every fix order.
- A multi-line initialization, a multi-line call, a comment or a directive keeps the gap as written.
- The rule owns the gap it keeps tight, so the separating rules ask for no blank line there, as [A keep rule and the other owners of its gap](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-keep-rule-and-the-other-owners-of-its-gap) says.
- Calls farther down follow [`ZA0004`](https://github.com/Nickelony/Stanza/wiki/ZA0004-Separate-invocation-runs) and, on a selected receiver, [`ZA0064`](https://github.com/Nickelony/Stanza/wiki/ZA0064-Keep-a-receiver-group-together).
