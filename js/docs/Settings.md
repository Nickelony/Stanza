# Settings

A value that two or more rules read is a `settings.stanza` key,
and a value that one rule reads is an option of that rule, which its ESLint schema validates.

```js
export default [
	{
		settings: {
			stanza: {
				assertionRoots: ['expect', 'assert', 'should'],
				testRoots: ['describe', 'it', 'test'],
				guardRoots: ['assert', 'invariant'],
				maxLooseRunSize: 3,
			},
		},
	},
];
```

- `assertionRoots`, `testRoots` and `guardRoots` are lists of exact names, and a list replaces the default.
  Python Stanza's roots are [globs](https://github.com/Nickelony/Stanza/blob/main/py/docs/Configuration.md).
- An explicit empty list means no roots, where the C# analyzers read an empty value as the defaults and spell no roots `none`.
  A key left out is the default.
- `maxLooseRunSize` is a whole number from 0 to 1000, where `0` disables both gather rules.
- A Stanza rule validates the settings when it runs, as ESLint does for the options of a rule.
  An unknown key, a list that is no array of strings and a limit that is no whole number in range throw an error that names the setting.
- Nothing validates the settings while no Stanza rule is enabled, as with the `none` preset, because nothing reads them.
- The [assertion](rules/separate-assertion-runs.md#settings), [test block](rules/separate-test-blocks.md#settings),
  [guard clause](rules/separate-guard-clauses.md#settings) and [loose run](rules/gather-loose-statements.md#settings) pages
  own the defaults and the rules that read each key.
