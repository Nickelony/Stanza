# stanza_declaration_typing

Controls whether [`ZA0002`](https://github.com/Nickelony/Stanza/wiki/ZA0002-Separate-declaration-runs) splits a run of declarations between those that write `var` and those that write a type.
Default: `split`.
Read by [`ZA0002`](https://github.com/Nickelony/Stanza/wiki/ZA0002-Separate-declaration-runs), and by [`ZA0052`](https://github.com/Nickelony/Stanza/wiki/ZA0052-Gather-a-run-of-loose-statements) and [`ZA0054`](https://github.com/Nickelony/Stanza/wiki/ZA0054-Keep-a-run-of-statements-together) where `ZA0002` is on.

| Value         | Effect                                                                           |
| ------------- | -------------------------------------------------------------------------------- |
| `split`       | declarations that write `var` and those that write a type form runs of their own |
| `merge`       | declarations that write `var` and those that write a type can share a run        |
| anything else | as `split`                                                                       |

```csharp
// Before
Prepare();
var first = 1;
var second = 2;
int third = 3;
string fourth = "";

// After with split
Prepare();

var first = 1;
var second = 2;

int third = 3;
string fourth = "";

// After with merge - the four declarations are one run
Prepare();

var first = 1;
var second = 2;
int third = 3;
string fourth = "";
```

- A deconstruction counts as `var` when every name is declared with `var`, as in `var (a, b) = Read();`,
  and as typed otherwise, as in `(int a, int b) = Read();`.
- `const` and `using` declarations are kinds of their own whatever this value says,
  because [`ZA0002`](https://github.com/Nickelony/Stanza/wiki/ZA0002-Separate-declaration-runs) reads them as a different job from declaring a value.
- [`ZA0003`](https://github.com/Nickelony/Stanza/wiki/ZA0003-Separate-same-type-declaration-runs) groups by the type as written, so it still tells `var` from `int` under `merge`.
- [`stanza_declaration_shapes`](https://github.com/Nickelony/Stanza/wiki/stanza_declaration_shapes) splits deconstructions from ordinary declarations, and its split applies on top of this one.
