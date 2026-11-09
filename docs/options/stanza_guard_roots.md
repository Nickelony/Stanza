# stanza_guard_roots

Controls which names mark a call as a throw helper: the root call's own name (`Guard(...)`),
or a name in its receiver chain (`Guard` in `Guard.NotNull(...)`).
Default: `none`.
Read by [`ZA0011`](https://github.com/Nickelony/Stanza/wiki/ZA0011-Separate-the-guard-clauses), which treats such a call as a guard clause.
[`ZA0010`](https://github.com/Nickelony/Stanza/wiki/ZA0010-Separate-the-base-call), [`ZA0009`](https://github.com/Nickelony/Stanza/wiki/ZA0009-Separate-a-jump-statement) and the rules that gather or keep a run (`ZA0052` to `ZA0055`)
read it to find where the guards end.

| Value                                     | Effect                                                                    |
| ----------------------------------------- | ------------------------------------------------------------------------- |
| a comma separated list                    | a call that holds a listed name is a guard, like `ThrowIfNull`            |
| `none`, empty, absent, or only separators | no root: only the calls of an `...Exception` type that start with `Throw` |

With `Guard` listed as a root, `Guard.NotNull(value);` is a guard, so ZA0011 writes a blank line below it;
under the default it is an ordinary call, and the rule writes nothing:

```csharp
// stanza_guard_roots = Guard
internal void M(object value)
{
	Guard.NotNull(value);

	Run(value);
}
```

- A name matches as one whole segment of the callee's dotted chain,
  so `Company.Guard.NotNull(...)` matches a bare `Guard`.
- An entry with a dot matches nothing, so list the bare name instead.
- Names are compared case-sensitively.
- A `Throw` method of a type that ends in `Exception`, such as `ArgumentNullException.ThrowIfNull(value)`,
  is a throw helper without a listed name.
- Only the lowercase `none` as the whole value lists no root, so `None` and a `none` beside other names are ordinary names.
