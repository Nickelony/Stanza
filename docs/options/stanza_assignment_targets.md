# stanza_assignment_targets

Controls whether [`ZA0008`](https://github.com/Nickelony/Stanza/wiki/ZA0008-Separate-assignment-runs) splits a run of assignments by target kind, which is what each one writes: a field, a property, a local or an element.
Default: `mixed`.
Read by [`ZA0008`](https://github.com/Nickelony/Stanza/wiki/ZA0008-Separate-assignment-runs), and by [`ZA0054`](https://github.com/Nickelony/Stanza/wiki/ZA0054-Keep-a-run-of-statements-together) where `ZA0008` is on.

| Value         | Effect                                                                      |
| ------------- | --------------------------------------------------------------------------- |
| `mixed`       | a run of assignments does not split by target kind                          |
| `split`       | a run of assignments splits between fields, properties, locals and elements |
| anything else | as `mixed`                                                                  |

```csharp
// Before - mixed leaves it as written
var total = 0;

_count = 0;
_name = "a";
Title = "b";
Size = 1;
total = 2;
total += 3;
_items[0] = "c";
_items[1] = "d";

// After with split
var total = 0;

_count = 0;
_name = "a";

Title = "b";
Size = 1;

total = 2;
total += 3;

_items[0] = "c";
_items[1] = "d";
```

The target kind comes from the symbol the left side writes, never from its name,
so `_count`, `s_count`, `t_count`, `m_count` and `count` are all fields when they are fields.

- A field is a field however it is reached: static or `[ThreadStatic]`, or through `this.`, `base.` or another instance.
  So is `point.X` of a struct local, a `readonly` field set in a constructor, `p->X` through a pointer, and the `field` of a C# 14 property.
- A field-like event written with `=`, such as `Changed = null;`, writes its delegate field, so it is a field.
- A member access takes the kind of the member it ends with, so `_options.Name = name;` writes the property `Name` and is a property,
  though it starts at a field.

Two neighbors that write through one root stay in one run whatever their kinds, so `split` leaves this as written:

```csharp
_start = CreateAnchor(startOffset);
_start.Movement = AnchorMovement.BeforeInsertion;

_end = CreateAnchor(endOffset);
_end.Movement = AnchorMovement.AfterInsertion;
```

- The root is the field, property, local or parameter a target starts from, after `this.`, `base.`, a type or a namespace,
  so `_start`, `this._start`, `_start.Movement` and `_start[0]` share the root `_start`.
- Neighbors with different roots split by kind. A chain through a call, such as `_start.Get().Movement`, has no root,
  and neither has a root that does not bind, so neither joins a neighbor by its root.
- A run is a chain of neighbor links, so `_a = 1; _a.B = 2; _c.D = 3;` is one run:
  `_a` and `_a.B` join by root, and `_a.B` and `_c.D` join as two properties.
- A property is a property whether it is static, init-only or implemented explicitly.
  A property that returns `ref`, as in `Slot = 1;`, writes through to a variable but names the property, so it is a property.
- A local or a parameter is one kind, ref and `scoped` locals, primary constructor parameters
  and the variables a lambda or a local function captures among them.
- An element is written through an indexer, `this[index]` included, an array, a span, an index from the end such as `items[^1]`,
  an indexer that returns `ref`, or a pointer, as `p[0]` and `*p` are.
  The form names an element, so it keeps its kind whether or not it binds.
- A compound assignment, `??=`, `++` and `--` take the kind of their target.
- A deconstruction whose targets are of one kind joins that kind, and a discard among them adds no kind.
  One of several kinds, such as `(_total, count) = Read();`, stands alone, so it ends the run above it and the run below it.
- A target that does not bind, as in code that does not compile, a member of a `dynamic` value, a discard such as `_ = value;`
  and a [subscription](https://github.com/Nickelony/Stanza/wiki/ZA0008-Separate-assignment-runs) have no kind.
- A target with no kind joins the runs on both sides, as it would under `mixed`, so code that stops compiling asks for no new blank line.
  So `_a = 1; _b = 2; _ = x; Count = 3; Count = 4;` with the fields `_a` and `_b` and the property `Count` is one run, with no split.
- A null-conditional assignment such as `item?.Name = name;` takes the kind of its member in C# 14.
  A Roslyn without C# 14 binds neither it nor `field`, so there they have no kind.
- A discarded call such as `_ = Read();` and a call with an `out` argument, such as `int.TryParse(text, out _count);`,
  stay calls that [`ZA0004`](https://github.com/Nickelony/Stanza/wiki/ZA0004-Separate-invocation-runs) judges.
- [Which rule owns which gap](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#one-run-of-statements-several-owners) says which rule owns the gaps beside a run,
  including the ones the split adds.
- Only `split` reads a semantic model, once for each assignment statement,
  and only while `ZA0008` is on, whether it or a rule that reads its runs asks.
  So `mixed` reads syntax alone.
