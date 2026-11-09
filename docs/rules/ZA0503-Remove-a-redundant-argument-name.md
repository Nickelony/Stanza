# ZA0503 - Remove a redundant argument name

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
- The exceptions for a conditional directive among the arguments, a member declared inside an `#if` block and names that bind another overload
  are those of [`ZA0501`](https://github.com/Nickelony/Stanza/wiki/ZA0501-Name-the-arguments-of-a-call), and a name that is redundant in one view could be needed in another.
- A comment beside the name stays, and the removal leaves no extra space beside it.
  A directive between the name and its colon, or after the colon, keeps its own line.
- A name goes only when the expression is an identifier or an `out` declaration of that name, so `this.width` keeps its name.
- [`stanza_named_argument_case`](https://github.com/Nickelony/Stanza/wiki/stanza_named_argument_case) decides whether letter case matters when the name is compared.
- Under [`stanza_named_argument_match`](https://github.com/Nickelony/Stanza/wiki/stanza_named_argument_match) `require`,
  the rule reports nothing, because [`ZA0501`](https://github.com/Nickelony/Stanza/wiki/ZA0501-Name-the-arguments-of-a-call) can write the names back.
- The rule ignores [`stanza_named_argument_min_arguments`](https://github.com/Nickelony/Stanza/wiki/stanza_named_argument_min_arguments),
  so a call of any size is judged.
- Before C# 7.2 a name cannot precede a positional argument, so the rule stops at the first name that stays.
  It also stops at the first unnamed argument that carries no name, which [`ZA0501`](https://github.com/Nickelony/Stanza/wiki/ZA0501-Name-the-arguments-of-a-call) names.
- Before C# 7.2 the fix for one name also removes the names before it.
