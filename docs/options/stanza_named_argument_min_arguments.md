# stanza_named_argument_min_arguments

Controls the fewest arguments a call must pass for [`ZA0501`](https://github.com/Nickelony/Stanza/wiki/ZA0501-Name-the-arguments-of-a-call) to name its unnamed arguments.
Default: `4`.
Read by [`ZA0501`](https://github.com/Nickelony/Stanza/wiki/ZA0501-Name-the-arguments-of-a-call).

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
  and the [ZA0501 page](https://github.com/Nickelony/Stanza/wiki/ZA0501-Name-the-arguments-of-a-call) owns the binding conditions.
- [`ZA0503`](https://github.com/Nickelony/Stanza/wiki/ZA0503-Remove-a-redundant-argument-name) removes a redundant name regardless of the call's argument count.
- [`ZA0502`](https://github.com/Nickelony/Stanza/wiki/ZA0502-Name-the-literal-arguments-of-a-call) names the literals of a call below this count.
