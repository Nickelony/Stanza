# ZA0020 - Separate yield runs

**Suggestion, off by default.**
Separates a [run](https://github.com/Nickelony/Stanza/wiki/Terms) of two or more neighboring `yield return` statements from its neighbors.

```csharp
// Before
Log("start");
yield return Header;
yield return Separator;
Log("body");

// After
Log("start");

yield return Header;
yield return Separator;

Log("body");
```

- Yield statements are one kind whatever they return, so `yield return 1;` and `yield return Build(item);` join one run.
- A `yield break` is no yield statement: it ends a run, and [`ZA0009`](https://github.com/Nickelony/Stanza/wiki/ZA0009-Separate-a-jump-statement) reads it as a jump.
- A lone yield statement stays tight, as a lone call does.
- A declaration on its own, directly above a run of yield statements, takes its blank line from this rule,
  and stays tight against the run while this rule is [off](https://github.com/Nickelony/Stanza/wiki/Terms).
- A gap where two runs meet goes to the rule with the lower ID, as [One run of statements, several owners](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#one-run-of-statements-several-owners) says.
- [`ZA0054`](https://github.com/Nickelony/Stanza/wiki/ZA0054-Keep-a-run-of-statements-together) removes the blank lines inside a run of yield statements where it is on.
