# stanza_local_function_modifiers

Controls whether [`ZA0021`](https://github.com/Nickelony/Stanza/wiki/ZA0021-Separate-local-function-runs) splits a run of local functions between `static` ones and the others.
Default: `merge`.
Read by [`ZA0021`](https://github.com/Nickelony/Stanza/wiki/ZA0021-Separate-local-function-runs), and by [`ZA0052`](https://github.com/Nickelony/Stanza/wiki/ZA0052-Gather-a-run-of-loose-statements) and [`ZA0054`](https://github.com/Nickelony/Stanza/wiki/ZA0054-Keep-a-run-of-statements-together) where `ZA0021` is on.

| Value         | Effect                                                         |
| ------------- | -------------------------------------------------------------- |
| `merge`       | `static` local functions and the others can share a run        |
| `split`       | `static` local functions and the others form runs of their own |
| anything else | as `merge`                                                     |

```csharp
// Before
Prepare();
static int Square(int v) => v * v;
static int Clamp(int v) => Math.Clamp(v, 0, 100);
int Scale(int v) => v * factor;
int Offset(int v) => v + shift;

// After
Prepare();

static int Square(int v) => v * v;
static int Clamp(int v) => Math.Clamp(v, 0, 100);
int Scale(int v) => v * factor;
int Offset(int v) => v + shift;

// After with split
Prepare();

static int Square(int v) => v * v;
static int Clamp(int v) => Math.Clamp(v, 0, 100);

int Scale(int v) => v * factor;
int Offset(int v) => v + shift;
```

- A `static` local function cannot capture a local or a parameter of its method,
  so the split sets apart the functions that stand alone from those that read their method.
- The other modifiers never split a run: `async`, `unsafe` and `extern` are ignored, and `static async` counts as `static`.
- A multi-line local function has no run, as [`ZA0021`](https://github.com/Nickelony/Stanza/wiki/ZA0021-Separate-local-function-runs) says, so it ends the runs beside it whatever this value says.
