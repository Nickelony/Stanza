# ZA0015 - Separate local function runs

**Suggestion, off by default.**
Separates a [run](https://github.com/Nickelony/Stanza/wiki/Terms) of two or more neighboring single-line local functions from its neighbors.

```csharp
// Before
return total;
static int Square(int v) => v * v;
static int Clamp(int v) => Math.Clamp(v, 0, 100);

// After
return total;

static int Square(int v) => v * v;
static int Clamp(int v) => Math.Clamp(v, 0, 100);
```

- A local function is single-line when its whole declaration, attributes and body included, stands on one line.
  A multi-line local function has no run: [`ZA0001`](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-multi-line-constructs) owns the gaps beside it.
- Local functions are one kind whatever they return, take or call, so an expression body and a single-line block body join one run.
- A lone local function stays tight.
- Where [`stanza_local_function_modifiers`](https://github.com/Nickelony/Stanza/wiki/stanza_local_function_modifiers) is `split`,
  `static` local functions and the others form runs of their own. Other modifiers, such as `async` and `unsafe`, never split a run.
- A declaration on its own, directly above a run of local functions, takes its blank line from this rule,
  and stays tight against the run while this rule is [off](https://github.com/Nickelony/Stanza/wiki/Terms).
- A gap where two runs meet goes to the rule with the lower ID, as [One run of statements, several owners](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#one-run-of-statements-several-owners) says.
- [`ZA0055`](https://github.com/Nickelony/Stanza/wiki/ZA0055-Keep-a-run-of-statements-together) removes the blank lines inside a run of local functions where it is on.
