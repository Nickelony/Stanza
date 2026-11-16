# ZA0064 - Keep dependent bindings together

**Suggestion, off by default.**
Removes the blank lines between neighboring single-line bindings that each read the binding above.

```csharp
// Before
string path = "input.dat";

byte[] bytes = System.IO.File.ReadAllBytes(path);

var stream = new System.IO.MemoryStream(bytes);

// After
string path = "input.dat";
byte[] bytes = System.IO.File.ReadAllBytes(path);
var stream = new System.IO.MemoryStream(bytes);

// Unchanged - destructuring ends the chain
var (catalog, store) = ReadPage();

var owner = ResolveOwner(catalog);
```

- Each declaration initializes one ordinary local name. The next initializer must read that local as a value.
  Calls, constructor arguments, member access, operators and awaited expressions can supply that read.
- The rule follows immediate neighbors only. An unrelated binding ends the chain; no statement is moved or searched for.
- A multi-line declaration ends the chain. A comment, a directive or a statement that shares its line with another ends it too.
- Deconstruction, multiple declarators, fields and resource declarations are outside the rule, so a blank line beside one stays.
- Type references, string contents, member names, `nameof` expressions, anonymous functions and queries do not supply a value read.
- An initializer that assigns or updates the preceding local, or passes it through a writable `ref` or `out` argument, is outside the rule.
  This includes dynamic calls; an `in` argument can supply a value read.
- A blank line between two [sections](https://github.com/Nickelony/Stanza/wiki/Terms) of a chain that already stand tight stays as written,
  and one fix removes every eligible blank line of its chain.
- Inside a chain the rule wins over the splits by declaration kind and by written type, and it asks for no blank line around the chain.
- A chain that ends in a binding a guard checks stays whole: this rule removes its inner blank lines,
  and the upper blank line of [`ZA0018`](https://github.com/Nickelony/Stanza/wiki/ZA0018-Separate-a-checked-stanza) goes above the whole chain.
- The other keep rules judge their own pairs.
  A binding whose upper blank line this rule removes is not a lone binding for [`ZA0060`](https://github.com/Nickelony/Stanza/wiki/ZA0060-Keep-a-jump-with-its-binding).
- A boundary that a separating rule asks for inside the chain moves above it, as [The boundary of a kept group](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#the-boundary-of-a-kept-group) says.
