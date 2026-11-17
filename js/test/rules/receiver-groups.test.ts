import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import { parseForESLint } from '@typescript-eslint/parser';
import { Linter } from 'eslint';
import tseslint from 'typescript-eslint';

import { readSharedSettings } from '../../src/analysis/environment.ts';
import {
	applyFix,
	assertGroupFix,
	createConfig,
	crlf,
	fix,
	fixOneByOne,
	lines,
	readPresetRules,
	readSilencedRuleIds,
	readSuppressedMessages,
	stable,
} from '../helpers.ts';

const separator = { 'separate-receiver-groups': 'error' } as const;
const keeper = { 'keep-receiver-groups-together': 'error' } as const;
const both = { ...separator, ...keeper };
const settings = (receiverComparison: 'root' | 'path' = 'path', receiverRoots = ['*']) => ({ stanza: { receiverRoots, receiverComparison } });

describe('receiver groups', () => {
	it('separates full paths while root comparison keeps them together', () => {
		const source = lines('object.call1();', 'object.call2();', 'object.child.call1();', 'object.child.call2();');
		const expected = lines('object.call1();', 'object.call2();', '', 'object.child.call1();', 'object.child.call2();');

		assert.equal(stable(source, separator, settings()), expected);
		assert.equal(stable(source, separator, settings('root')), source);
	});

	it('selects any normalized segment independently of comparison', () => {
		const source = lines('object.child.a();', 'object.child.b();', 'object.c();', 'object.d();');
		const expected = lines('object.child.a();', 'object.child.b();', '', 'object.c();', 'object.d();');

		assert.equal(stable(source, separator, settings('root', ['child'])), expected);
		assert.equal(stable(source, separator, settings('root', ['this'])), source);
		assert.equal(stable(source, separator, settings('path', [])), source);
	});

	it('does not separate three differing singleton roots', () => {
		const source = lines('logger.info();', 'db.add(cart);', 'cache.clear();');
		assert.equal(stable(source, separator, settings()), source);
	});

	it('separates a singleton beside a group of two', () => {
		const source = lines('logger.info();', 'db.add();', 'db.save();', 'cache.clear();');
		assert.equal(stable(source, separator, settings()), lines('logger.info();', '', 'db.add();', 'db.save();', '', 'cache.clear();'));
	});

	for (const operation of ['object.a = 1;', 'object.a += 1;', 'object.a++;', '--object.a;', 'object.on(event, handler);', 'await object.flush();']) {
		it('groups mixed operations: ' + operation, () => {
			const source = lines('prepare();', operation, '', 'object.save();', 'done();');
			const expected = lines('prepare();', '', operation, 'object.save();', '', 'done();');

			assert.equal(stable(source, both, settings()), expected);
		});
	}

	it('compares assignment owners rather than assigned members or RHS values', () => {
		const source = lines('object.child = createChild();', 'object.child.configure();', 'object.child.save();');

		assert.equal(stable(source, both, settings()), lines('object.child = createChild();', '', 'object.child.configure();', 'object.child.save();'));
		assert.equal(stable(source, both, settings('root')), source);
	});

	it('keeps mixed family edges tight and transfers separation above the group', () => {
		const source = lines('prepare();', 'object.a = 1;', 'object.b = 2;', 'object.save();', 'done();');
		const expected = lines('prepare();', '', 'object.a = 1;', 'object.b = 2;', 'object.save();', 'done();');

		assert.equal(stable(source, { ...keeper, 'separate-assignment-runs': 'error' }, settings()), expected);
	});

	it('keeps selected db gaps while authored logger gaps survive', () => {
		const source = lines('logger.info(a);', '', 'logger.info(b);', 'db.add(cart);', '', 'await db.flush();', 'done();');
		assert.equal(stable(source, keeper, settings('root', ['db'])), source.replace('db.add(cart);\n\n', 'db.add(cart);\n'));
	});

	it('preserves intentional sections while joining isolated gaps', () => {
		const source = lines('db.a();', 'db.b();', '', 'db.c();', 'db.d();');

		assert.equal(stable(source, keeper, settings()), source);
		assert.equal(stable(lines('db.a();', '', 'db.b();', '', 'db.c();'), keeper, settings()), lines('db.a();', 'db.b();', 'db.c();'));
	});

	it('leaves declarations and bare locals outside receiver groups', () => {
		const source = lines('const client = createClient();', '', 'client.timeout = timeout;', 'client.start();', 'value = client;');
		assert.equal(stable(source, keeper, settings()), source);
	});

	for (const receiver of ['db[0]', 'getDb()', 'db?.child', 'db.child?.nested']) {
		it('excludes unsupported receiver ' + receiver, () => {
			const source = lines('prepare();', receiver + '.a();', '', receiver + '.b();', 'done();');
			assert.equal(stable(source, both, settings()), source);
		});
	}

	for (const receiver of ['(db)', 'db!', '(db as Database)', '(<Database>db)', '(db satisfies Database)']) {
		it('accepts transparent receiver ' + receiver, () => {
			const source = lines('prepare();', receiver + '.a();', '', receiver + '.b();', 'done();');
			const expected = lines('prepare();', '', receiver + '.a();', receiver + '.b();', '', 'done();');

			assert.equal(stable(source, both, settings()), expected);
		});
	}

	it('accepts private names', () => {
		const source = lines('class C {', ' #db;', ' f() {', '  prepare();', '  this.#db.a();', '', '  this.#db.b();', '  done();', ' }', '}');
		const expected = lines('class C {', ' #db;', ' f() {', '  prepare();', '', '  this.#db.a();', '  this.#db.b();', '', '  done();', ' }', '}');

		assert.equal(stable(source, both, settings()), expected);
	});

	it('strips a leading qualifier exactly once', () => {
		const source = lines('this.self.db.a();', 'this.self.cache.b();', 'self.self.other.c();');
		assert.equal(stable(source, separator, settings('root')), source);
	});

	for (const scope of ['function f() {', 'class C { static {', 'namespace N {', 'switch (x) { case 1:']) {
		it('reads statement scopes in ' + scope, () => {
			const close = scope.startsWith('class') ? '} }' : '}';
			const source = lines(scope, ' db.a();', '', ' db.b();', ' done();', close);

			assert.equal(stable(source, keeper, settings()), source.replace('db.a();\n\n', 'db.a();\n'));
		});
	}

	it('preserves comment and multiline barriers', () => {
		for (const middle of ['// note', '// eslint-disable-next-line no-console', '// @ts-expect-error']) {
			const source = lines('db.a();', '', middle, 'db.b();', 'done();');
			assert.equal(stable(source, keeper, settings()), source);
		}

		const source = lines('db.a();', '', 'db.b(', ' options,', ');', 'done();');
		assert.equal(stable(source, keeper, settings()), source);
	});

	it('does not capture assertions, tests or guard helpers under wildcard selection', () => {
		for (const name of ['assert.ok', 'expect', 'invariant', 'describe']) {
			const source = lines(name + '(a);', '', name + '(b);', 'done();');
			assert.equal(stable(source, keeper, settings()), source);
		}
	});

	it('lets initialization keeping transfer a receiver edge above its protected pair', () => {
		const source = lines('prepare();', '', 'const db = createDb();', '', 'db.a();', 'db.b();', 'done();');
		const expected = lines('prepare();', '', 'const db = createDb();', 'db.a();', 'db.b();', '', 'done();');

		assert.equal(stable(source, { ...both, 'keep-initialization-with-calls': 'error' }, settings()), expected);
	});

	it('preserves path edges against gathering and global keeping', () => {
		const source = lines('object.a = 1;', 'object.b = 2;', 'object.child.a = 3;', 'object.child.b = 4;');
		const expected = source.replace('object.child.a', '\nobject.child.a');

		const rules = {
			...separator,
			'gather-loose-statements': 'error',
			'keep-statement-runs-together': 'error',
			'separate-assignment-runs': 'error',
		} as const;

		assert.equal(stable(source, rules, settings()), expected);
		assert.equal(stable(expected, rules, settings()), expected);
	});

	it('preserves host disable directives', () => {
		const source = lines('/* eslint-disable stanza/keep-receiver-groups-together */', 'db.a();', '', 'db.b();', 'done();');
		assert.equal(stable(source, keeper, settings()), source);
	});

	it('leaves a gap a directive silences for a separating rule to that rule, so no other rule takes it', () => {
		const source = lines('/* eslint-disable stanza/separate-invocation-runs */', 'prepare();', 'await db.flush();', 'await db.refresh();', 'done();');
		const rules = { 'separate-invocation-runs': 'error', 'separate-await-runs': 'error' } as const;

		assert.equal(stable(source, rules, settings()), source);
		const withoutConsumer = fix(source, { 'separate-await-runs': 'error' }, settings());
		assert.notEqual(withoutConsumer, source);
	});

	it('keeps CRLF fixes stable', () => {
		const source = crlf(lines('db.a();', '', 'db.b();', 'done();'));
		assert.equal(stable(source, both, settings()), crlf(lines('db.a();', 'db.b();', '', 'done();')));
	});

	it('retains native validation errors', () => {
		assert.equal(readSharedSettings({}).receiverComparison, 'root');
		assert.throws(() => readSharedSettings({ stanza: { receiverComparison: 'method' } }), /must be "root" or "path"/);
	});

	it('settles all receiver-consumer combinations in both comparison modes', () => {
		const names = ['separate-invocation-runs', 'separate-receiver-groups', 'keep-statement-runs-together', 'keep-receiver-groups-together'] as const;
		const source = lines('prepare();', 'object.a();', '', 'await object.b();', 'object.child.a();', '', 'await object.child.b();', 'done();');

		for (let mask = 0; mask < 16; mask += 1) {
			const rules = Object.fromEntries(names.filter((_, index) => mask & (1 << index)).map((name) => [name, 'error' as const]));

			for (const comparison of ['root', 'path'] as const) {
				const options = settings(comparison);
				const result = stable(source, rules, options);

				assert.equal(stable(source, Object.fromEntries(Object.entries(rules).reverse()), options), result);
			}
		}
	});

	it('settles strong presets with opt-in receiver behavior', () => {
		const source = lines(
			'async function f() {',
			' db.a();',
			'',
			' await db.b();',
			' db.child.a = 1;',
			' db.child.b = 2;',
			' db.child.save();',
			' done();',
			'}',
		);

		stable(source, readPresetRules('strong', true), settings());
	});
});

it('claims every tight inner gap of a compact mixed receiver group', () => {
	const source = lines('object.a = 1;', 'object.b = 2;', 'object.save();', 'object.close();');
	assert.equal(stable(source, { ...keeper, 'separate-assignment-runs': 'error' }, settings()), source);
});

it('puts the fix of the whole group on each report, which writes the text a batch fix writes', () => {
	const source = lines('db.a();', '', 'db.b();', '', 'db.c();', 'db.d();');
	const expected = lines('db.a();', 'db.b();', 'db.c();', 'db.d();');
	const config = createConfig(keeper, settings());
	const reports = new Linter().verify(source, config, { filename: 'file.ts' });

	assert.equal(reports.length, 2);
	assertGroupFix(source, reports, expected);
	assert.equal(stable(source, keeper, settings()), expected);
});

it('reads each statement of a group a number of times linear in its size, and gives a long group one fix', () => {
	const counts: number[] = [];

	for (const size of [200, 400]) {
		let reads = 0;

		const parser = {
			...tseslint.parser,

			parseForESLint(code: string, options: Parameters<typeof parseForESLint>[1]) {
				const parsed = parseForESLint(code, options);

				for (const statement of parsed.ast.body) {
					if (statement.type === 'ExpressionStatement') {
						const { expression } = statement;

						Object.defineProperty(statement, 'expression', {
							get: () => {
								reads += 1;
								return expression;
							},
						});
					}
				}

				return parsed;
			},
		};

		const source = lines(...Array.from({ length: size }, (_, index) => 'db.call' + index + '();').flatMap((line) => [line, '']));

		const rules = {
			...both,
			'separate-invocation-runs': 'error',
			'keep-statement-runs-together': 'error',
			'gather-loose-statements': 'error',
		} as const;

		const first = createConfig(rules, settings())[0];
		const reports = new Linter().verify(source, [{ ...first, languageOptions: { ...first?.languageOptions, parser } }], { filename: 'file.ts' });

		assert.equal(reports.length, size - 1);
		assert.ok(reports.every((report) => report.ruleId === 'stanza/keep-receiver-groups-together'));
		assert.equal(reports.filter((report) => report.fix !== undefined).length, 1);

		counts.push(reads);
	}

	assert.ok((counts[0] ?? 0) > 0);
	assert.ok((counts[1] ?? 0) <= (counts[0] ?? 0) * 2.5, `reads grew from ${counts[0]} to ${counts[1]} as the group doubled`);
});

for (const [keeperName, body] of Object.entries({
	'keep-jump-with-binding': ['const result = compute();', '', 'return result;'],
	'keep-initialization-with-calls': ['const client = createClient();', '', 'client.start();', 'finish();'],
})) {
	for (const extra of [
		{},
		{ 'gather-loose-statements': 'error' as const },
		{ 'keep-statement-runs-together': 'error' as const },
		{ 'separate-invocation-runs': 'error' as const, 'keep-statement-runs-together': 'error' as const },
	] as Record<string, Linter.RuleEntry>[]) {
		it('settles a receiver-created upper anchor with ' + keeperName + ' and ' + Object.keys(extra).join('+'), () => {
			const source = lines('function f() {', 'db.first();', 'db.second();', ...body, '}');

			const expected = source
				.replace('db.second();\n', 'db.second();\n\n')
				.replace('compute();\n\n', 'compute();\n')
				.replace('createClient();\n\n', 'createClient();\n');

			const rules = { ...separator, [keeperName]: 'error' as const, ...extra };

			assert.equal(stable(source, rules, settings('root', ['db'])), expected);
			const config = createConfig(rules, settings('root', ['db']));

			for (const pick of ['first', 'last'] as const) {
				assert.equal(fixOneByOne(source, config, pick), expected);
			}
		});
	}
}

it('does not let a return steal a binding from a gathered declaration run', () => {
	const source = lines('function f() {', 'const first = computeFirst();', '', 'const result = compute();', '', 'return result;', '}');
	const rules = { 'separate-declaration-runs': 'error', 'keep-statement-runs-together': 'error', 'keep-jump-with-binding': 'error' } as const;

	assert.equal(stable(source, rules, settings()), source.replace('computeFirst();\n\n', 'computeFirst();\n'));
});

for (const name of ['separate-invocation-runs', 'separate-receiver-groups']) {
	it('ends receiver groups at same-line barriers for ' + name, () => {
		const source = lines('prepare();', 'db.a(); db.b();', 'done();');
		assert.equal(stable(source, { [name]: 'error' }, settings('root', ['db'])), source);
	});
}

it('keeps the earlier assignment owner at an explicit same-key path boundary', () => {
	const source = lines('object.a = 1;', 'object.b = 2;', 'object.child.a = 3;', 'object.child.b = 4;');

	const reports = new Linter().verify(source, createConfig({ ...separator, 'separate-assignment-runs': 'error' }, settings()), {
		filename: 'file.ts',
	});

	assert.deepEqual(
		reports.map((report) => report.ruleId),
		['stanza/separate-assignment-runs'],
	);
});

for (const [keeperName, body] of Object.entries({
	'keep-jump-with-binding': ['const result = compute();', '', 'return result;'],
	'keep-initialization-with-calls': ['const client = createClient();', '', 'client.start();', 'finish();'],
})) {
	for (const [separatorName, prefix] of Object.entries({
		'separate-invocation-runs': ['db.first();', 'db.second();'],
		'separate-checked-stanzas': ['const value = load();', 'if (!value) return;'],
		'separate-multi-line-constructs': ['prepare(', 'options,', ');'],
	})) {
		it('settles an existing native upper anchor from ' + separatorName + ' with ' + keeperName, () => {
			const source = lines('function f() {', ...prefix, ...body, '}');

			const expected = source
				.replace(prefix.at(-1) + '\n', prefix.at(-1) + '\n\n')
				.replace('compute();\n\n', 'compute();\n')
				.replace('createClient();\n\n', 'createClient();\n');

			assert.equal(stable(source, { [separatorName]: 'error', [keeperName]: 'error' }, settings('root', ['db'])), expected);
		});
	}
}

for (const [keeperName, body] of Object.entries({
	'keep-guard-with-binding': ['const value = get();', '', 'if (!value) return;', 'use(value);'],
	'keep-accumulator-with-loop': ['const items = [];', '', 'for (const item of input) {', 'items.push(item);', '}', 'use(items);'],
	'keep-assertion-with-binding': ['const value = get();', '', 'assert(value);', 'use(value);'],
})) {
	it('gathers loose declarations without erasing the transferred border of ' + keeperName, () => {
		const prefix = ['const first = 1;', '', 'const second = 2;', '', 'const third = 3;', ''];
		const source = lines('function f() {', ...prefix, ...body, '}');

		const expected = lines('function f() {', ...prefix, ...body.filter((line) => line !== ''), '}');

		assert.equal(
			stable(source, { 'separate-declaration-runs': 'error', 'gather-loose-statements': 'error', [keeperName]: 'error' }, settings()),
			expected,
		);
	});
}

it('gathers four loose declarations once a protected group boundary moves above them', () => {
	const source = lines(
		'function f() {',
		'const first = 1;',
		'',
		'const second = 2;',
		'',
		'const third = 3;',
		'',
		'const fourth = 4;',
		'',
		'const value = get();',
		'',
		'if (!value) return;',
		'use(value);',
		'}',
	);

	const expected = lines(
		'function f() {',
		'const first = 1;',
		'const second = 2;',
		'const third = 3;',
		'const fourth = 4;',
		'',
		'const value = get();',
		'if (!value) return;',
		'use(value);',
		'}',
	);

	assert.equal(
		stable(source, { 'separate-declaration-runs': 'error', 'gather-loose-statements': 'error', 'keep-guard-with-binding': 'error' }, settings()),
		expected,
	);
});

describe('receiver sections', () => {
	const objectRoots = (receiverComparison: 'root' | 'path' = 'root') => ({
		stanza: { receiverRoots: ['obj'], receiverComparison, maxLooseRunSize: 1 },
	});

	it('reads no invocation run across a protected section, so no border is written around it', () => {
		const source = lines('prepare();', 'obj.member = 1;', 'obj.call();', '', 'obj.otherCall();', 'obj.member = 2;', 'finish();');

		const rules = {
			...keeper,
			'separate-invocation-runs': 'error',
			'separate-assignment-runs': 'error',
			'keep-statement-runs-together': 'error',
			'gather-loose-statements': 'error',
		} as const;

		assert.equal(stable(source, rules, objectRoots()), source);
	});

	it('keeps authored sections through a fix of the first report, of the last one, or of all of them', () => {
		const source = lines('obj.head = 0;', '', 'obj.a = 1;', 'obj.call();', '', 'obj.otherCall();', 'obj.b = 2;', '', 'obj.tail = 3;');
		const expected = lines('obj.head = 0;', 'obj.a = 1;', 'obj.call();', '', 'obj.otherCall();', 'obj.b = 2;', 'obj.tail = 3;');
		const rules = { ...keeper, 'separate-invocation-runs': 'error', 'keep-statement-runs-together': 'error' } as const;
		const config = createConfig(rules, settings('root', ['obj']));

		for (const pick of ['first', 'last'] as const) {
			assert.equal(
				fixOneByOne(source, config, pick, (text) => assert.ok(text.includes('obj.call();\n\nobj.otherCall();'))),
				expected,
			);
		}

		assert.equal(stable(source, rules, settings('root', ['obj'])), expected);
	});

	const consumers = ['separate-invocation-runs', 'separate-assignment-runs', 'keep-statement-runs-together', 'gather-loose-statements'];

	for (const receiverComparison of ['root', 'path'] as const) {
		for (const separate of [false, true]) {
			for (let mask = 0; mask < 1 << consumers.length; mask += 1) {
				const enabled = consumers.filter((_, index) => mask & (1 << index));
				const names = [...enabled, ...(separate ? ['separate-receiver-groups'] : [])];

				it(`keeps a mixed receiver section by ${receiverComparison} with ${names.length === 0 ? 'the keeper alone' : names.join(', ')}`, () => {
					const source = lines('obj.member = 1;', 'obj.call();', '', 'obj.otherCall();', 'obj.member = 2;');
					const rules = { ...keeper, ...Object.fromEntries(names.map((name) => [name, 'error'])) } as Record<string, Linter.RuleEntry>;

					assert.equal(stable(source, rules, objectRoots(receiverComparison)), source);
					assert.equal(stable(source, Object.fromEntries(Object.entries(rules).reverse()), objectRoots(receiverComparison)), source);
				});
			}
		}
	}

	for (const [open, close] of [
		['function f() {', '}'],
		['class C { static {', '} }'],
		['namespace N {', '}'],
		['switch (value) { case 1:', '}'],
	] as const) {
		it(`keeps receiver sections and a lone unit between two of them inside ${open}`, () => {
			const source = lines(open, 'obj.a = 1;', 'obj.b();', '', 'obj.c();', '', 'obj.d();', 'obj.e = 2;', close);

			const rules = {
				...keeper,
				'separate-invocation-runs': 'error',
				'separate-assignment-runs': 'error',
				'keep-statement-runs-together': 'error',
				'gather-loose-statements': 'error',
			} as const;

			assert.equal(stable(source, rules, objectRoots()), source);
		});
	}
});

describe('receiver rules under a directive', () => {
	const appRoots = settings('root', ['app']);

	it('hands a silenced gap back to the rules that read runs, as if the keeper were off', () => {
		const source = lines(
			'/* eslint-disable stanza/keep-receiver-groups-together */',
			'obj.a = 1;',
			'obj.call();',
			'',
			'obj.otherCall();',
			'obj.b = 2;',
		);

		const weak = { 'separate-invocation-runs': 'error', 'keep-statement-runs-together': 'error' } as const;

		const released = lines(
			'/* eslint-disable stanza/keep-receiver-groups-together */',
			'obj.a = 1;',
			'',
			'obj.call();',
			'obj.otherCall();',
			'',
			'obj.b = 2;',
		);

		const roots = settings('root', ['obj']);

		assert.equal(stable(source, weak, roots), released);
		assert.equal(stable(source, { ...weak, ...keeper }, roots), released);
	});

	it('keeps the blank line of a silenced gap out of the fix of the rest of its group', () => {
		const source = lines('app.use(a);', '', 'app.use(b /* eslint-disable-line stanza/keep-receiver-groups-together */);', '', 'app.use(c);');
		const expected = lines('app.use(a);', '', 'app.use(b /* eslint-disable-line stanza/keep-receiver-groups-together */);', 'app.use(c);');

		const linter = new Linter();
		const [report, ...others] = linter.verify(source, createConfig(keeper, appRoots), { filename: 'file.ts' });

		assert.deepEqual(others, []);

		assert.deepEqual(
			readSuppressedMessages(linter).map((message) => message.ruleId),
			['stanza/keep-receiver-groups-together'],
		);

		assert.deepEqual(
			readSuppressedMessages(linter).map((message) => message.fix),
			[undefined],
		);

		assert.ok(report?.fix);
		assert.equal(applyFix(source, report.fix), expected);
		assert.equal(stable(source, keeper, appRoots), expected);
	});

	it('reports nothing where an eslint-disable comment silences the separating rule', () => {
		const source = lines('/* eslint-disable stanza/separate-receiver-groups */', 'logger.info();', 'db.add();', 'db.save();', 'cache.clear();');

		assert.deepEqual(readSilencedRuleIds(source, separator, settings()), ['stanza/separate-receiver-groups', 'stanza/separate-receiver-groups']);
		assert.equal(fix(source, separator, settings()), source);
	});
});
