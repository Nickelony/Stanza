# ZA0501 - Name the arguments of a call

**Suggestion, off by default.**
Writes the name of the bound parameter before each unnamed argument of a call that passes at least [`stanza_named_argument_min_arguments`](https://github.com/Nickelony/Stanza/wiki/stanza_named_argument_min_arguments) arguments,
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
- An argument list with a conditional directive (`#if`, `#elif`, `#else` or `#endif`)
  among its arguments, or between the tokens of an argument, is left alone.
- Such a directive can shift the positions after it in another [view](https://github.com/Nickelony/Stanza/wiki/Terms).
  A name written from one view could then bind to the wrong parameter in the other.
- A `#region`, a `#pragma`, or a directive inside a bracket or a brace of an argument such as a lambda body,
  shifts no position, so the call is judged as usual.
- Below C# 7.2 a named argument cannot precede a positional one, so only the leading arguments that carry their parameter's name stay unnamed,
  and every argument after the first one named is named too.
- A call that the names would bind to another overload, or make ambiguous, is left alone,
  and so is one whose names would bind a call that takes it in a lambda argument to a different overload.
- A call to a member declared inside an `#if` block, or whose parameter list holds one, is left alone.
  Another view can declare the member with other parameter names.
- [`stanza_named_argument_case`](https://github.com/Nickelony/Stanza/wiki/stanza_named_argument_case) decides what counts as the same name.
- A call is left alone when any argument binds to no parameter or to a `params` parameter,
  because naming some arguments and not others leaves a shape no fix can finish.
- A call the compiler cannot bind as a whole is left alone.
- Calls through a delegate, array element accesses and attribute arguments are left alone.
  So are a conditional element access such as `grid?[row, column]` and an indexer written in an object initializer.
- A call inside an expression tree is left alone, because an expression tree may not contain a named argument.
- A call in the inner sequence of a query's `join` is named, because that sequence is an ordinary argument.
- [`ZA0503`](https://github.com/Nickelony/Stanza/wiki/ZA0503-Remove-a-redundant-argument-name) removes the name of each argument that carries it,
  so from C# 7.2 on the rules never undo each other under `allow`.
- [`ZA0502`](https://github.com/Nickelony/Stanza/wiki/ZA0502-Name-the-literal-arguments-of-a-call) names the literals alone,
  and leaves a call this rule reports to this rule.
