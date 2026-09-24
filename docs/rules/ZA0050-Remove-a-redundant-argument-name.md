# ZA0050 - Remove a redundant argument name

**Suggestion, off by default.**
Removes the name from an argument such as `width: width` when the argument is an identifier or an `out` declaration that carries its parameter's name,
and sits at that parameter's own position.

```csharp
// Before - the name and the argument beside it are the same word
Draw(width: width, height: height, smooth: true, path: path);

// After - every argument that carries its parameter's name loses the repeat
Draw(width, height, smooth: true, path);
```

- The rule reads the arguments in order and stops at the first one that is not at its parameter's own position,
  or that fills a `params` array.
- A call that writes no name is left alone.
- A comment beside the name stays, and the removal leaves no extra space beside it.
- A call stays as written when dropping the names would bind it to a different overload,
  or would bind a call that takes it in a lambda argument to a different overload.
- A name goes only when the expression is an identifier or an `out` declaration of that name, so `this.width` keeps its name.
- [`stanza_named_argument_case`](https://github.com/Nickelony/Stanza/wiki/stanza_named_argument_case) decides whether letter case matters when the name is compared.
- Under [`stanza_named_argument_match`](https://github.com/Nickelony/Stanza/wiki/stanza_named_argument_match) `require`,
  the rule reports nothing, because [`ZA0049`](https://github.com/Nickelony/Stanza/wiki/ZA0049-Name-the-arguments-of-a-call) can write the names back.
- The rule ignores [`stanza_named_argument_count`](https://github.com/Nickelony/Stanza/wiki/stanza_named_argument_count),
  so a call of any size is judged.
- Before C# 7.2 a name cannot precede a positional argument, so the rule stops at the first name that stays.
  It also stops at the first unnamed argument that carries no name, which [`ZA0049`](https://github.com/Nickelony/Stanza/wiki/ZA0049-Name-the-arguments-of-a-call) names.
- Before C# 7.2 the fix for one name also removes the names before it.
