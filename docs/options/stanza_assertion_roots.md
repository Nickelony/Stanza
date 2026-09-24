# stanza_assertion_roots

Controls which names begin an assertion chain: the root call's own name (`Assert(...)`),
or a name in its receiver chain (`Verify` in `Verify.That(...)`).
Default: `Assert`, `Expect`, `Should`.
Read by [`ZA0001`](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-declaration-and-invocation-runs), which groups invocation runs by them.

| Value                             | Effect                                                      |
| --------------------------------- | ----------------------------------------------------------- |
| a comma separated list            | **replaces** the defaults, so list every root the file uses |
| absent, empty, or only separators | the defaults                                                |

With `Verify` listed as a root, every statement that starts with `Verify` forms one run, however many methods it calls,
so ZA0001 writes a blank line on each side of the run; under the default roots it writes neither:

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
