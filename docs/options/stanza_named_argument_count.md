# stanza_named_argument_count

Controls the fewest arguments a call must pass for [`ZA0049`](https://github.com/Nickelony/Stanza/wiki/ZA0049-Name-the-arguments-of-a-call) to name its unnamed arguments.
Default: `4`.
Read by [`ZA0049`](https://github.com/Nickelony/Stanza/wiki/ZA0049-Name-the-arguments-of-a-call).

| Value                                           | Effect                                                       |
| ----------------------------------------------- | ------------------------------------------------------------ |
| `0`                                             | the rule reports no call                                     |
| a number from 1 to 1000                         | a call is judged when it passes at least that many arguments |
| below zero, above 1000, absent, or not a number | the default                                                  |

```csharp
// Unchanged - three arguments, below the threshold
Draw(640, 480, "image.png");

// Before - four arguments, at the threshold
Draw(640, 480, true, "image.png");

// After
Draw(width: 640, height: 480, smooth: true, path: "image.png");
```

- The count covers the arguments written at the call site, not the parameters the target member declares.
- An argument that already has a name counts as a passed argument.
- A call the compiler cannot bind as a whole is ignored,
  and the [ZA0049 page](https://github.com/Nickelony/Stanza/wiki/ZA0049-Name-the-arguments-of-a-call) owns the binding conditions.
- [`ZA0050`](https://github.com/Nickelony/Stanza/wiki/ZA0050-Remove-a-redundant-argument-name) removes a redundant name regardless of the call's argument count.
