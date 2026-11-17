# stanza_declaration_shapes

Controls whether [`ZA0002`](https://github.com/Nickelony/Stanza/wiki/ZA0002-Separate-declaration-runs) splits a run of declarations between deconstruction declarations and ordinary ones.
Default: `merge`.
Read by [`ZA0002`](https://github.com/Nickelony/Stanza/wiki/ZA0002-Separate-declaration-runs), and by [`ZA0052`](https://github.com/Nickelony/Stanza/wiki/ZA0052-Gather-a-run-of-loose-statements) and [`ZA0055`](https://github.com/Nickelony/Stanza/wiki/ZA0055-Keep-a-run-of-statements-together) where `ZA0002` is on.

| Value         | Effect                                                   |
| ------------- | -------------------------------------------------------- |
| `merge`       | deconstruction and ordinary declarations can share a run |
| `split`       | deconstruction declarations form runs of their own       |
| anything else | as `merge`                                               |

```csharp
// Before - merge leaves it as written
var (first, second) = Read();
var (third, fourth) = Read();
var count = 2;
var limit = 4;

// After with split - the deconstructions form a run of their own
var (first, second) = Read();
var (third, fourth) = Read();

var count = 2;
var limit = 4;
```

- A deconstruction declares every target, as in `var (a, b) = Read()` or `(int a, int b) = Read()`.
- A tuple value assigned to one name is an ordinary declaration.
- Constants and resource declarations are kinds of their own whatever this value says.
  [`stanza_declaration_typing`](https://github.com/Nickelony/Stanza/wiki/stanza_declaration_typing) decides whether inferred types and explicit types split.
- A run must hold two or more declarations before [`ZA0002`](https://github.com/Nickelony/Stanza/wiki/ZA0002-Separate-declaration-runs) asks for its boundaries.
- The other tools read the setting under their own shapes: [JS Stanza](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-declaration-runs.md) splits array patterns, object patterns and plain identifiers three ways,
  and [Python Stanza](https://github.com/Nickelony/Stanza/blob/main/py/docs/Configuration.md#declaration-shapes) splits unpacking from other bindings two ways, as C# does.
