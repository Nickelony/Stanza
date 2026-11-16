import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import { parseForESLint } from '@typescript-eslint/parser';
import { Linter } from 'eslint';
import tseslint from 'typescript-eslint';

import { assertGroupFix, assertSilencedWithoutFix, createConfig, crlf, lines, readPresetRules, stable } from '../helpers.ts';

const keeper = { 'keep-dependent-bindings-together': 'error' } as const;

describe('dependent binding chains', () => {
	it('joins an awaited producer, awaited member call and constructor argument', () => {
		const source = lines(
			'const file = await fileHandle.getFileAsync();',
			'',
			'const bytes = await file.arrayBufferAsync();',
			'',
			'const stream = new MemoryStream(bytes);',
		);

		const expected = lines(
			'const file = await fileHandle.getFileAsync();',
			'const bytes = await file.arrayBufferAsync();',
			'const stream = new MemoryStream(bytes);',
		);

		assert.equal(stable(source, keeper), expected);
		assert.equal(stable(crlf(source), keeper), crlf(expected));
	});

	for (const initializer of [
		'transform(value)',
		'value.child',
		'value[0]',
		'value + 1',
		'{ value }',
		'{ [value]: 1 }',
		'typeof value',
		'(value as Input)',
		'((value) => value)(value)',
		'class extends value {}',
	]) {
		it('recognizes an immediate runtime read in ' + initializer, () => {
			const source = lines('const value = load();', '', 'let next = ' + initializer + ';');
			assert.equal(stable(source, keeper), source.replace('load();\n\n', 'load();\n'));
		});
	}

	for (const initializer of [
		'other.value',
		'{ value: 1 }',
		'"value"',
		'() => value',
		'function () { return value; }',
		'class { method() { return value; } }',
		'((value) => value)(other)',
		'(value = other)',
	]) {
		it('ignores a property, type, deferred or write-only mention in ' + initializer, () => {
			const source = lines('let value = load();', '', 'const next = ' + initializer + ';');
			assert.equal(stable(source, keeper), source);
		});
	}

	it('does not read type annotations as value dependencies', () => {
		const source = lines('const value = load();', '', 'const next: typeof value = other;');
		assert.equal(stable(source, keeper), source);
	});

	for (const [name, expression, joins] of [
		['Component', '<Component />', true],
		['component', '<component.Item />', true],
		['component', '<component />', false],
		['Item', '<Other.Item />', false],
		['value', '<Other value="value" />', false],
		['value', '<Other value={value} />', true],
	] as const) {
		it('reads JSX runtime values in ' + expression, () => {
			const source = lines('const ' + name + ' = load();', '', 'const node = ' + expression + ';');
			const expected = joins ? source.replace('load();\n\n', 'load();\n') : source;
			const linter = new Linter();
			const options = { filename: 'file.tsx', stats: true };
			const result = linter.verifyAndFix(source, createConfig(keeper), options);

			assert.deepEqual(result.messages, []);
			assert.equal(result.output, expected);
			assert.ok(linter.getFixPassCount() <= 1);
		});
	}

	for (const first of [
		'const { value } = load();',
		'const [value] = load();',
		'const value = load(), other = 1;',
		'let value;',
		'using value = load();',
		'export const value = load();',
		'declare const value: unknown;',
	]) {
		it('excludes a nonordinary producer ' + first, () => {
			const source = lines(first, '', 'const next = transform(value);');
			assert.equal(stable(source, keeper), source);
		});
	}

	for (const next of [
		'const { next } = transform(value);',
		'const [next] = transform(value);',
		'const next = transform(value), other = 1;',
		'using next = transform(value);',
		'export const next = transform(value);',
	]) {
		it('excludes a nonordinary consumer ' + next, () => {
			const source = lines('const value = load();', '', next);
			assert.equal(stable(source, keeper), source);
		});
	}

	for (const barrier of ['// section', '// eslint-disable-next-line no-console', '// @ts-expect-error']) {
		it('preserves the comment barrier ' + barrier, () => {
			const source = lines('const value = load();', '', barrier, 'const next = transform(value);');
			assert.equal(stable(source, keeper), source);
		});
	}

	it('ends at multiline and joined statement gaps', () => {
		for (const source of [
			lines('const value = load(', ' options,', ');', '', 'const next = transform(value);'),
			lines('const value = load();', '', 'const next = transform(', ' value,', ');'),
			lines('const value = load(); const next = transform(value);'),
		]) {
			assert.equal(stable(source, keeper), source);
		}
	});

	it('does not search ahead or treat a shared earlier name as a direct chain', () => {
		const source = lines('const first = load();', '', 'const unrelated = other();', '', 'const third = transform(first);');
		assert.equal(stable(source, keeper), source);
	});

	it('preserves authored sections of two dependent bindings', () => {
		const source = lines(
			'const first = load();',
			'const second = transform(first);',
			'',
			'const third = transform(second);',
			'const fourth = transform(third);',
		);

		assert.equal(stable(source, keeper), source);
	});

	for (const weak of ['keep-statement-runs-together', 'gather-loose-statements']) {
		it('protects a mixed-kind dependent section from ' + weak + ', which reads no declaration run around it', () => {
			const source = lines('prepare();', 'let a = load();', 'const b = use(a);', '', 'const c = use(b);', 'let d = use(c);');
			const rules = { ...keeper, [weak]: 'error', 'separate-declaration-runs': 'error' } as Record<string, Linter.RuleEntry>;

			assert.equal(stable(source, rules), source);
			assert.notEqual(stable(source, { [weak]: 'error', 'separate-declaration-runs': 'error' }), source);
		});
	}

	it('puts the fix of the whole chain on its first report, which writes the text a batch fix writes', () => {
		const source = lines(
			'const first = load();',
			'',
			'const second = transform(first);',
			'',
			'const third = transform(second);',
			'const fourth = transform(third);',
		);

		const expected = source.replaceAll('\n\n', '\n');
		const reports = new Linter().verify(source, createConfig(keeper), { filename: 'file.ts' });

		assert.equal(reports.length, 2);
		assertGroupFix(source, reports, expected);
		assert.equal(stable(source, keeper), expected);
	});

	it('preserves destructuring shape separation even when its last binding feeds the next declaration', () => {
		const source = lines(
			'const { catalog, store } = usePage();',
			'const { config } = useAppState();',
			'const preference = pairPreference(pair, config);',
			'const owner = pairOwner(catalog, pair, config);',
		);

		const expected = source.replace('useAppState();\n', 'useAppState();\n\n');

		assert.equal(stable(source, { ...keeper, 'separate-declaration-runs': 'error' }, { stanza: { declarationShapes: 'split' } }), expected);
	});

	it('keeps a changing declaration-kind chain together and transfers separation above the whole chain', () => {
		const source = lines(
			'audit();',
			'const value = load();',
			'',
			'const second = normalize(value);',
			'let next = transform(second);',
			'var last = finish(next);',
			'done();',
		);

		const expected = lines(
			'audit();',
			'',
			'const value = load();',
			'const second = normalize(value);',
			'let next = transform(second);',
			'var last = finish(next);',
			'done();',
		);

		assert.equal(stable(source, { ...keeper, 'separate-declaration-runs': 'error' }), expected);
	});

	for (const jumpKeeper of [false, true]) {
		it('separates a jump from the prospective dependent chain with jump keeping ' + jumpKeeper, () => {
			const source = lines(
				'function f() {',
				'const first = load();',
				'',
				'const second = transform(first);',
				'',
				'const third = transform(second);',
				'return third;',
				'}',
			);

			const expected = lines(
				'function f() {',
				'const first = load();',
				'const second = transform(first);',
				'const third = transform(second);',
				'',
				'return third;',
				'}',
			);

			const rules = { ...keeper, 'separate-jump-statements': 'error', ...(jumpKeeper ? { 'keep-jump-with-binding': 'error' } : {}) } as Record<
				string,
				Linter.RuleEntry
			>;

			assert.equal(stable(source, rules), expected);
		});
	}

	it('does not let a jump keeper steal a last binding whose upper gap the chain removes', () => {
		const source = lines('function f() {', 'const first = load();', '', 'const result = transform(first);', '', 'return result;', '}');
		const expected = source.replace('load();\n\n', 'load();\n');

		assert.equal(stable(source, { ...keeper, 'keep-jump-with-binding': 'error' }), expected);
	});

	it('does not let initialization keeping steal a constructor binding from the chain', () => {
		const source = lines('const bytes = load();', '', 'const stream = new MemoryStream(bytes);', '', 'stream.start();');
		const expected = source.replace('load();\n\n', 'load();\n');

		assert.equal(stable(source, { ...keeper, 'keep-initialization-with-calls': 'error' }), expected);
	});

	it('reports a gap whose directive silences it without a fix, so the directive counts as used and stays', () => {
		const source = lines(
			'const first = load();',
			'',
			'// eslint-disable-next-line stanza/keep-dependent-bindings-together',
			'const second = transform(first);',
		);

		assertSilencedWithoutFix(source, keeper, ['stanza/keep-dependent-bindings-together']);
		assert.equal(stable(source, keeper), source);
	});

	for (const expression of [
		'(value = replacement(), use(value))',
		'(use(value), value = replacement())',
		'value++',
		'++value',
		'value += 1',
		'({ value } = use(value))',
	]) {
		it('does not connect a value clobbered inside the next initializer: ' + expression, () => {
			const source = lines('let value = load();', '', 'const next = ' + expression + ';');
			assert.equal(stable(source, keeper), source);
		});
	}

	it('joins the whole chain when a directive silences the assertion keeper that would take its last gaps', () => {
		const source = lines(
			'/* eslint-disable stanza/keep-assertion-with-binding */',
			'function f() {',
			'const a = load();',
			'',
			'const b = build(a);',
			'',
			'const c = build(b);',
			'',
			'assert(b && c);',
			'}',
		);

		const expected = lines(
			'/* eslint-disable stanza/keep-assertion-with-binding */',
			'function f() {',
			'const a = load();',
			'const b = build(a);',
			'const c = build(b);',
			'',
			'assert(b && c);',
			'}',
		);

		const rules = { ...keeper, 'keep-assertion-with-binding': 'error' } as const;
		const reports = new Linter().verify(source, createConfig(rules), { filename: 'file.ts' });

		assert.equal(stable(source, rules), expected);

		assert.deepEqual(
			reports.map((report) => report.ruleId),
			['stanza/keep-dependent-bindings-together', 'stanza/keep-dependent-bindings-together'],
		);

		assertGroupFix(source, reports, expected);
	});

	it('keeps the blank line of a silenced gap out of the fix of the rest of the chain', () => {
		const source = lines(
			'const a = load();',
			'',
			'const b = build(a /* eslint-disable-line stanza/keep-dependent-bindings-together */);',
			'',
			'const c = build(b);',
		);

		const expected = source.replace(');\n\nconst c', ');\nconst c');

		assertSilencedWithoutFix(source, keeper, ['stanza/keep-dependent-bindings-together']);
		assert.equal(stable(source, keeper), expected);
	});

	for (const [source, joins] of [
		[lines('const meta = describe();', '', 'const here = import.meta.url;'), false],
		[lines('const meta = describe();', '', 'const here = meta.url;'), true],
		[lines('function f() {', 'const target = make();', '', 'const kind = new.target;', '}'), false],
		[lines('function f() {', 'const target = make();', '', 'const kind = target.kind;', '}'), true],
	] as const) {
		it(`${joins ? 'joins' : 'leaves apart'} the binding above ${source.split('\n').find((line) => line.startsWith('const kind') || line.startsWith('const here'))}`, () => {
			assert.equal(stable(source, keeper), joins ? source.replace('();\n\n', '();\n') : source);
		});
	}

	for (const initializer of [
		'(value! = use(value))',
		'(value!)++',
		'(value as any)++',
		'((value as any) = use(value))',
		'({ x: value! } = obj, use(value))',
		'([value!] = obj, use(value))',
		'({ x: value! = use(value) } = obj)',
	]) {
		it(`leaves apart a binding the next initializer overwrites through ${initializer}`, () => {
			const source = lines('let value = load();', '', 'const next = ' + initializer + ';');
			assert.equal(stable(source, keeper), source);
		});
	}

	for (const initializer of [
		'({ [value]: other } = obj)',
		'({ x: other = value } = obj)',
		'([other = value] = obj)',
		'({ x: value!.child } = obj)',
		'([value![index]] = obj)',
	]) {
		it(`joins a binding the next initializer reads inside the assignment target ${initializer}`, () => {
			const source = lines('let value = load();', '', 'const next = ' + initializer + ';');
			assert.equal(stable(source, keeper), source.replace('\n\n', '\n'));
		});
	}

	it('joins a chain above a guard whose callback parameter shadows its last binding, and separates the guard', () => {
		const source = lines(
			'function f(items) {',
			'const value = load();',
			'',
			'const next = build(value);',
			'',
			'if (items.some(next => next.id)) return;',
			'work();',
			'}',
		);

		const rules = { ...keeper, 'keep-guard-with-binding': 'error', 'separate-checked-stanzas': 'error' } as const;
		const expected = source.replace('load();\n\n', 'load();\n').replace('return;\nwork', 'return;\n\nwork');

		assert.equal(stable(source, rules), expected);
	});

	for (const checkedKeeper of ['keep-assertion-with-binding', 'keep-guard-with-binding']) {
		it('keeps the declaration run above the chain tight once ' + checkedKeeper + ' and the loose prefix above it join', () => {
			const check = checkedKeeper === 'keep-assertion-with-binding' ? 'assert(c);' : 'if (!c) return;';

			const source = lines(
				'function f() {',
				'const x = 1;',
				'',
				'const y = 2;',
				'',
				'const z = 3;',
				'const a = load();',
				'const b = build(a);',
				'const c = build(b);',
				check,
				'use(c);',
				'}',
			);

			const rules = {
				...keeper,
				[checkedKeeper]: 'error',
				'separate-checked-stanzas': 'error',
				'separate-declaration-runs': 'error',
				'keep-statement-runs-together': 'error',
			} as Record<string, Linter.RuleEntry>;

			const expected = stable(source, rules);

			assert.ok(!expected.includes('const z = 3;\n\nconst a'), 'classified-run upper protection was released');
			assert.ok(!expected.includes('const x = 1;\n\nconst y'), 'eligible prefix gaps were left behind');
		});
	}

	for (const gather of [false, true]) {
		for (const runKeeper of [false, true]) {
			for (const shape of ['merge', 'split']) {
				it('settles declaration/receiver/checked policy combinations: ' + [gather, runKeeper, shape].join('/'), () => {
					const rules: Record<string, Linter.RuleEntry> = {
						...keeper,
						'separate-declaration-runs': 'error',
						'separate-multi-line-constructs': 'error',
						'separate-invocation-runs': 'error',
						'separate-checked-stanzas': 'error',
						'separate-receiver-groups': 'error',
						'keep-receiver-groups-together': 'error',
						'keep-guard-with-binding': 'error',
						'keep-assertion-with-binding': 'error',
						'keep-jump-with-binding': 'error',
						'keep-initialization-with-calls': 'error',
						...(gather ? { 'gather-loose-statements': 'error' } : {}),
						...(runKeeper ? { 'keep-statement-runs-together': 'error' } : {}),
					};

					const options = { stanza: { receiverRoots: ['db'], receiverComparison: 'path', declarationShapes: shape } };

					const source = lines(
						'function f() {',
						'db.open();',
						'db.load();',
						'const first = load();',
						'',
						'const second = transform(first);',
						'',
						'const result = finish(second);',
						'if (!result) return;',
						'use(result);',
						'}',
					);

					const expected = stable(source, rules, options);

					assert.equal(stable(source, Object.fromEntries(Object.entries(rules).reverse()), options), expected);
				});
			}
		}
	}

	it('supports a legal same-name var rebinding that reads its preceding value', () => {
		const source = lines('var value = load();', '', 'var value = transform(value);');
		assert.equal(stable(source, keeper), source.replace('\n\n', '\n'));
	});

	it('is opt-in in every preset', () => {
		for (const level of ['none', 'minimal', 'moderate', 'strong'] as const) {
			assert.equal('keep-dependent-bindings-together' in readPresetRules(level), false);
		}
	});

	for (const [open, close] of [
		['function f() {', '}'],
		['class C { static {', '} }'],
		['namespace N {', '}'],
		['switch (value) { case 1:', '}'],
	]) {
		it('reads ordinary statement scope ' + open, () => {
			const source = lines(open!, 'const first = load();', '', 'const next = transform(first);', close!);
			assert.equal(stable(source, keeper), source.replace('load();\n\n', 'load();\n'));
		});
	}

	it('reads the name of each binding a number of times linear in the length of the chain, and gives the chain one fix', () => {
		const counts: number[] = [];

		for (const size of [200, 400]) {
			let reads = 0;

			const config = createConfig({
				...keeper,
				'separate-declaration-runs': 'error',
				'keep-statement-runs-together': 'error',
				'gather-loose-statements': 'error',
				'separate-jump-statements': 'error',
				'keep-jump-with-binding': 'error',
			});

			const first = config[0]!;

			const parser = {
				...tseslint.parser,

				parseForESLint(code: string, options: Parameters<typeof parseForESLint>[1]) {
					const parsed = parseForESLint(code, options);

					for (const statement of parsed.ast.body) {
						for (const declaration of statement.type === 'VariableDeclaration' ? statement.declarations : []) {
							const { id } = declaration;
							const { name } = id as { name?: string };

							Object.defineProperty(id, 'name', {
								get: () => {
									reads += 1;
									return name;
								},
							});
						}
					}

					return parsed;
				},
			};

			const source = lines(
				...Array.from(
					{ length: size },
					(_, index) => 'const value' + index + ' = ' + (index === 0 ? 'load()' : 'transform(value' + (index - 1) + ')') + ';',
				).flatMap((line) => [line, '']),
			);

			const reports = new Linter().verify(source, [{ ...first, languageOptions: { ...first.languageOptions, parser } }], { filename: 'file.ts' });

			assert.equal(reports.length, size - 1);
			assert.ok(reports.every((report) => report.ruleId === 'stanza/keep-dependent-bindings-together'));
			assert.equal(reports.filter((report) => report.fix !== undefined).length, 1);

			counts.push(reads);
		}

		assert.ok(counts[0]! > 0);
		assert.ok(counts[1]! <= counts[0]! * 2.5, `reads grew from ${counts[0]} to ${counts[1]} as the chain doubled`);
	});
});
