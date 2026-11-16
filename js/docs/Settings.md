# Settings

A value that two or more rules read is a `settings.stanza` key,
and a value that one rule reads is an option of that rule, which its ESLint schema validates.

```js
export default [
	{
		settings: {
			stanza: {
				declarationShapes: 'merge',
				receiverComparison: 'root',
				receiverRoots: [],
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
- `declarationShapes` accepts `merge` or `split`, with `merge` as its default; the [declaration rule](rules/separate-declaration-runs.md#settings) defines the shapes.
  `split` gives array patterns, object patterns and plain names a run key each,
  where the C# and Python settings split destructuring from plain names only.
- `receiverComparison` accepts `root` or `path`, with `root` as its default; the
  [invocation rule](rules/separate-invocation-runs.md#settings) defines identity.
- `receiverRoots` lists receiver names,
  each compared exactly with every segment of a receiver path once one leading `this`, `self` or `super` is removed.
- A private member is listed in `receiverRoots` with its `#`, as `'#db'`, and `'*'` selects every receiver;
  the [invocation rule](rules/separate-invocation-runs.md#settings) defines which receivers qualify and how they group.
- `maxLooseRunSize` is a whole number from 0 to 1000, where `0` disables both gather rules.
- A Stanza rule validates the settings when it runs, as ESLint does for the options of a rule.
- An unknown key, an invalid root list, a declaration shape or receiver comparison outside its allowed values,
  or a limit that is not a whole number in range throws an error that names the setting.
- A root list that holds a blank name, or a name with spaces around it, is invalid too, since no name could match it.
- Nothing validates the settings while no Stanza rule is enabled, as with the `none` preset, because nothing reads them.
- The [assertion](rules/separate-assertion-runs.md#settings), [test block](rules/separate-test-blocks.md#settings),
  [guard clause](rules/separate-guard-clauses.md#settings) and [loose run](rules/gather-loose-statements.md#settings) pages
  own their defaults and the rules that read each key.

