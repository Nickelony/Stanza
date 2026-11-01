# separate-assignment-runs

**Level: strong.**
Separates a run of two or more neighboring assignments from the statements beside it,
so the lines that set state read apart from the calls around them.

```ts
// Before
validate(input);
this.name = input.name;
this.size = input.size;
initialize();

// After
validate(input);

this.name = input.name;
this.size = input.size;

initialize();
```

- An assignment is a statement that uses `=`, a compound operator such as `+=` or `??=`, or an increment or decrement.
  So `a = 1;`, `this.b = 2;`, `c[0] += 3;` and `d++;` are one run, whatever they write.
- A right side that awaits or calls still reads as an assignment, so `data = await load();` joins the run.
- A declaration such as `const a = 1;` is not an assignment, and [`separate-declaration-runs`](separate-declaration-runs.md) judges it.
- A lone assignment stays tight, as a lone call does.
- A declaration on its own, directly above a run of assignments, takes its blank line from this rule.
- Where another run rule asks for the same gap, the first enabled rule of the [owner order](../Rule-map.md#one-owner-per-gap) reports it.
- [`keep-statement-runs-together`](keep-statement-runs-together.md) removes the blank lines inside a run of assignments where it is also enabled.

## C# counterpart

[ZA0008 - Separate assignment runs](https://github.com/Nickelony/Stanza/wiki/ZA0008-Separate-assignment-runs),
without [`stanza_assignment_targets`](https://github.com/Nickelony/Stanza/wiki/stanza_assignment_targets),
because the split by field, property, local and element needs a symbol and the default of that key does not split.
