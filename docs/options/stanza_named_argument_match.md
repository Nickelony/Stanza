# stanza_named_argument_match

Controls whether an argument that is an identifier or an `out` declaration carrying its parameter's name may stay unnamed.
Default: `allow`.
Read by the argument-naming rules, such as [`ZA0049`](https://github.com/Nickelony/Stanza/wiki/ZA0049-Name-the-arguments-of-a-call).

| Value         | Effect                                                                                 |
| ------------- | -------------------------------------------------------------------------------------- |
| `allow`       | may stay unnamed, as `Draw(width, ...)` does for a parameter named `width`             |
| `require`     | the argument is written `width: width`                                                 |
| anything else | as `allow`                                                                             |

```csharp
// allow (default) - the identifiers that carry their parameter's name stay unnamed
Draw(width, height, smooth: true, path);

// require - the name is written even where the identifier already carries it
Draw(width: width, height: height, smooth: true, path: path);
```

[`stanza_named_argument_case`](https://github.com/Nickelony/Stanza/wiki/stanza_named_argument_case) decides whether an identifier carries its parameter's name.

[`ZA0050`](https://github.com/Nickelony/Stanza/wiki/ZA0050-Remove-a-redundant-argument-name) reads the same key the other way.
Under `allow`, it drops the name of each argument that carries its parameter's name,
so `Draw(width: width)` becomes `Draw(width)`; its page says where it stops. Under `require`, it reports nothing.
