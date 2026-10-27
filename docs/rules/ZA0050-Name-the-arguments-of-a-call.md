# ZA0050 - Name the arguments of a call

**Suggestion, off by default.**
Writes the name of the bound parameter before each unnamed argument of a call that passes at least [`stanza_named_argument_count`](https://github.com/Nickelony/Stanza/wiki/stanza_named_argument_count) arguments,
except an identifier or an `out` declaration that carries its parameter's name while [`stanza_named_argument_match`](https://github.com/Nickelony/Stanza/wiki/stanza_named_argument_match) is `allow`.

```csharp
// Before - four arguments, and nothing but their order says which is which
Draw(640, 480, true, "image.png");

// After - each argument names the parameter it binds to
Draw(width: 640, height: 480, smooth: true, path: "image.png");
```

- A call is an invocation, an object creation, a `this` or `base` constructor initializer,
  the base call of a primary constructor, or an indexer access.
- The rule reports once per call, at its argument list, and its fix names every argument the rule asks for.
- An argument that already has a name is left as it is.
- Below C# 7.2 a named argument cannot precede a positional one, so only the leading arguments that carry their parameter's name stay unnamed,
  and every argument after the first one named is named too.
- A call that the names would bind to another overload, or make ambiguous, is left alone,
  and so is one whose names would bind a call that takes it in a lambda argument to a different overload.
- [`stanza_named_argument_case`](https://github.com/Nickelony/Stanza/wiki/stanza_named_argument_case) decides what counts as the same name.
- A call is left alone when any argument binds to no parameter or to a `params` parameter,
  because naming some arguments and not others leaves a shape no fix can finish.
- A call the compiler cannot bind as a whole is left alone.
- Calls through a delegate, array element accesses and attribute arguments are left alone.
  So are a conditional element access such as `grid?[row, column]` and an indexer written in an object initializer.
- A call inside an expression tree is left alone, because an expression tree may not contain a named argument.
- A call in the inner sequence of a query's `join` is named, because that sequence is an ordinary argument.
- [`ZA0051`](https://github.com/Nickelony/Stanza/wiki/ZA0051-Remove-a-redundant-argument-name) removes the name of each argument that carries it,
  so from C# 7.2 on the rules never undo each other under `allow`.
