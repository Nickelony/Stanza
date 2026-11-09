# ZA0502 - Name the literal arguments of a call

**Suggestion, off by default.**
Writes the name of the bound parameter before each `true`, `false`, `null` or `default` argument of a call that passes
two or more arguments, as [`stanza_named_argument_literals`](https://github.com/Nickelony/Stanza/wiki/stanza_named_argument_literals) picks the words.

```csharp
// Before - nothing at the call says what `true` and `null` stand for
Save(path, true, null);

// After - each literal names the parameter it binds to
Save(path, overwrite: true, backup: null);
```

- A call is what [`ZA0501`](https://github.com/Nickelony/Stanza/wiki/ZA0501-Name-the-arguments-of-a-call) defines, and the rule reports once per call, at its argument list;
  its fix names every literal the rule asks for.
- A call that passes one argument is left alone, as in `SetEnabled(true)`.
- Numbers, strings and characters stay unnamed, as in `Math.Max(a, 0)`.
- `default` covers the literal and `default(T)`, because neither says what it stands for at the call.
- A `null!` or a `default!` counts as the `null` or `default` it quiets, because the `!` adds nothing the call says.
- The rule names the literal alone and leaves the other arguments as written,
  because from C# 7.2 on a named argument at its own position needs no other argument named.
- Below C# 7.2 a named argument cannot precede a positional one,
  so the first literal and every argument after it are named, and a call whose later arguments cannot be named is left alone.
- An argument that already has a name is left as it is.
- An argument that fills a `params` array stays unnamed, though the other literals of its call are named.
- A call through a delegate is left alone, because its parameter names say nothing, such as the `arg1` of a `Func`.
  So are a conditional element access such as `grid?[true, 1]` and an indexer written in an object initializer.
- The exceptions for a conditional directive among the arguments, a member declared inside an `#if` block, an expression tree
  and names that bind another overload are those of [`ZA0501`](https://github.com/Nickelony/Stanza/wiki/ZA0501-Name-the-arguments-of-a-call), which owns them.
- [`ZA0501`](https://github.com/Nickelony/Stanza/wiki/ZA0501-Name-the-arguments-of-a-call) owns a call it reports,
  because naming every argument covers the literals, so the two never report one call.
  This rule works with `ZA0501` off, and reports a call below its
  [`stanza_named_argument_min_arguments`](https://github.com/Nickelony/Stanza/wiki/stanza_named_argument_min_arguments).
- [`ZA0503`](https://github.com/Nickelony/Stanza/wiki/ZA0503-Remove-a-redundant-argument-name) removes only the name of an identifier
  or an `out` declaration that carries its parameter's name, so it never removes the name of a literal.
