# stanza_named_argument_case

Controls whether case matters when an identifier matches the name of its parameter.
Default: `sensitive`.
Read by the argument-naming rules, such as [`ZA0050`](https://github.com/Nickelony/Stanza/wiki/ZA0050-Name-the-arguments-of-a-call).

| Value         | Effect                                                                                                                                                                            |
| ------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `sensitive`   | `Width` for a parameter named `width` is written `width: Width`                                                                                                                   |
| `insensitive` | `Width` for a parameter named `width` may stay unnamed, unless [`stanza_named_argument_match`](https://github.com/Nickelony/Stanza/wiki/stanza_named_argument_match) is `require` |
| anything else | as `sensitive`                                                                                                                                                                    |

```csharp
// sensitive (default) - each identifier differs from its parameter's name by case, so each is named
Draw(width: Width, height: Height, smooth: Smooth, path: Path);

// insensitive - each argument carries its parameter's name whatever the case of its letters
Draw(Width, Height, Smooth, Path);
```

`sensitive` compares the names character by character, and `insensitive` also ignores letter case, the same way in every culture.

Only a bare identifier counts, including one declared by `out`.
Member accesses such as `this.width` and `options.Width` are expressions, so this key does not affect them.

This key compares only the identifier, never the name before the colon.

[`stanza_named_argument_match`](https://github.com/Nickelony/Stanza/wiki/stanza_named_argument_match) decides whether an identifier that carries its parameter's name may stay unnamed.
