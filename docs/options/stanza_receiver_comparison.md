# stanza_receiver_comparison

Controls whether selected receivers match by the first name of their path or by their full owner path.
Default: `root`.
Read by [`ZA0004`](https://github.com/Nickelony/Stanza/wiki/ZA0004-Separate-invocation-runs), [`ZA0010`](https://github.com/Nickelony/Stanza/wiki/ZA0010-Separate-receiver-groups) and [`ZA0064`](https://github.com/Nickelony/Stanza/wiki/ZA0064-Keep-a-receiver-group-together).

| Value         | Effect                                                                               |
| ------------- | ------------------------------------------------------------------------------------ |
| `root`        | the first name of the receiver path is compared, after a leading `this` or `base`    |
| `path`        | the complete receiver path is compared, without the called method or assigned member |
| anything else | as `root`                                                                            |

With `stanza_receiver_roots = item` and [`ZA0010`](https://github.com/Nickelony/Stanza/wiki/ZA0010-Separate-receiver-groups) enabled:

```csharp
// Before - root leaves it as written
item.Call1();
item.Call2();
item.Child.Call1();
item.Child.Call2();

// After with path
item.Call1();
item.Call2();

item.Child.Call1();
item.Child.Call2();
```

- [`stanza_receiver_roots`](https://github.com/Nickelony/Stanza/wiki/stanza_receiver_roots) chooses which receivers take part; this setting chooses when two of them are the same.
  Neither setting enables a spacing rule.
- A call to `item.Child.Save()` and a write to `item.Child.Name` share the owner path `item.Child` and the root `item`.
- A write to `item.Child` has the owner `item`, so in `path` mode it differs from a call to `item.Child.Configure()`.
- Only a leading `this` or `base` is ignored. Later members and escaped identifiers keep their names.
- A path is the written syntax; aliases and runtime identity are not inferred.
