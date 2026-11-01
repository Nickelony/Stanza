# separate-declaration-runs

**Level: moderate.**
Separates a run of two or more neighboring declarations of one kind from the statements beside it.

```ts
// Before
const limit = 3;
const retries = 5;
let attempts = 0;
log(attempts);

// After
const limit = 3;
const retries = 5;

let attempts = 0;
log(attempts);
```

- A declaration is a `const`, `let`, `var`, `using` or `await using` statement, or a `type` alias.
- Declarations group by kind, so `const` and `let` are two runs. An `export` and a `declare` modifier make a kind of their own.
- A lone declaration stays tight against the statement below it, unless that statement starts a run of another rule.
- A declaration that awaits, such as `const data = await load();`, is a declaration, and [`separate-await-runs`](separate-await-runs.md) leaves it alone.

## C# counterpart

[ZA0002 - Separate declaration runs](https://github.com/Nickelony/Stanza/wiki/ZA0002-Separate-declaration-runs)
and [ZA0003 - Separate same type declaration runs](https://github.com/Nickelony/Stanza/wiki/ZA0003-Separate-same-type-declaration-runs).
