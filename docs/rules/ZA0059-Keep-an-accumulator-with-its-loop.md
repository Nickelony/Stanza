# ZA0059 - Keep an accumulator with its loop

**Suggestion, off by default.**
Removes the blank lines between the single-line seeds of a name and the loop below them that builds it.

```csharp
// Before
Check(cart);
decimal total = 0m;

foreach (var line in cart.Lines)
	total += line.Price;
Show(total);

// After
Check(cart);
decimal total = 0m;
foreach (var line in cart.Lines)
	total += line.Price;
Show(total);

// Unchanged - the loop only uses the object, it does not build it
Check(messages);
var client = new Client();

foreach (var message in messages)
	client.Send(message);
Show(client);
```

- The seed and the loop that fills it are one step: the seed means nothing without the loop.
- A seed is a single-line local declaration of one name, or a statement that assigns one name with `=`,
  whose value reads no variable, such as `0`, `0m`, `null`, `default`, `string.Empty`, `[]`, `new()` or `new List<int>()`.
- A type, a method, a constant and the member a dot names are not variables, so `new StringBuilder()` and `Decimal.Zero` are seeds.
- The loop is a `for`, a `foreach`, a `while` or a `do`.
  Its executing body must assign the seeded binding, step it with `++` or `--`, or call a builder method on it.
  Assignments can use `=` or a compound operator.
- Builder methods are `Add`, `AddRange`, `Append`, `AppendLine`, `AppendFormat`, `Insert`, `TryAdd`, `Enqueue` and `Push`.
- Parentheses and supported null suppression around a target or builder receiver are transparent.
- Updates in nested control flow and nested loops qualify.
  Lambda bodies, local function bodies and type declarations do not qualify, and an update to a shadowed binding does not build the seed.
- An assignment that reads the seed only as an index does not build it.
  Any other method call only uses the object, so the blank line stays.
- The single-line seeds directly above the loop that it builds, `var sum = 0;` and `var count = 0;` above a loop that updates both,
  form one group with the loop, so the rule removes the blank lines between them too.
  The group ends at the first statement above that is no seed of the loop.
- A comment or a directive in the gap, a trailing comment included, keeps the blank line.
- The rule owns the gap it keeps tight, so the separating rules ask for no blank line there, as [A keep rule and the other owners of its gap](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-keep-rule-and-the-other-owners-of-its-gap) says.
- The rule judges the statements of a block, of a switch section and at the top level of a file,
  and leaves the gap below the loop to the other rules.
- A boundary that a separating rule asks for inside the group moves above it, as [The boundary of a kept group](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#the-boundary-of-a-kept-group) says.
