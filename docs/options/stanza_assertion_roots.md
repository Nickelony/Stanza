# stanza_assertion_roots

Controls which names begin an assertion chain: the root call's own name (`Assert(...)`),
or a name in its receiver chain (`Verify` in `Verify.That(...)`).
Default: `Assert`, `Expect`, `Should`.
Read by [`ZA0005`](https://github.com/Nickelony/Stanza/wiki/ZA0005-Separate-assertion-runs), which groups assertion runs by them,
and by `ZA0004` and `ZA0006`, which leave an assertion to it.

| Value                             | Effect                                                        |
| --------------------------------- | ------------------------------------------------------------- |
| a comma separated list            | **replaces** the defaults, so list every root the file uses   |
| `none`                            | no roots: no call is an assertion, so `ZA0005` groups nothing |
| absent, empty, or only separators | the defaults                                                  |

With `Verify` listed as a root, every statement that starts with `Verify` forms one run, however many methods it calls,
so ZA0005 writes a blank line on each side of the run; under the default roots it writes neither:

```csharp
// stanza_assertion_roots = Verify
internal void M(int value)
{
	int actual = 1;

	Verify.That(value);
	Verify.AreEqual(1, value);

	return;
}
```

- A name matches as one whole segment of the callee's dotted chain,
  so `NUnit.Framework.Assert.That(...)` matches a bare `Assert`.
- An entry with a dot matches nothing, so list the bare name instead.
- Names are compared case-sensitively.
- Only the lowercase `none` as the whole value lists no root, so `None` and a `none` beside other names are ordinary names.
- An empty value stands for the defaults here, so a stray empty line cannot switch assertion grouping off.
  The JS and Python tools read an explicit empty list as no roots, which C# spells `none`.
