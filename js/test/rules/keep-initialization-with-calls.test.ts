import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import { fix, fixRule, lines, readPresetRules, readSilencedRuleIds, settled, stable } from '../helpers.ts';

const rule = { 'keep-initialization-with-calls': 'error' } as const;

describe('keep-initialization-with-calls', () => {
	it('joins an initialization that opens a stanza to its first direct call', () => {
		const code = lines('print("test");', '', 'const item = new Item();', '', 'item.call1();', 'item.call2();', 'item.call3();');
		const expected = lines('print("test");', '', 'const item = new Item();', 'item.call1();', 'item.call2();', 'item.call3();');

		assert.equal(fix(code, rule), expected);
		assert.equal(fix(expected, rule), expected);
	});

	it('leaves an initialization tight against the statement above unchanged', () => {
		const code = lines('print("test");', 'const item = new Item();', '', 'item.call1();', 'item.call2();');
		assert.equal(fix(code, rule), code);
	});

	it('leaves a multi-line first call apart', () => {
		const code = lines('print("test");', '', 'const item = new Item();', '', 'item.call1(', '  first,', '  second,', ');', '', 'item.call2();');
		assert.equal(fix(code, rule), code);
	});

	it('keeps comments on either edge in place', () => {
		for (const code of [
			lines('prepare();', '', '// the item', 'const item = new Item();', '', 'item.call1();'),
			lines('prepare();', '', 'const item = new Item();', '', '// initialize', 'item.call1();'),
			lines('prepare();', '', 'const item = new Item(); // own it', '', 'item.call1();'),
		]) {
			assert.equal(fix(code, rule), code);
		}
	});

	it('supports an awaited factory and an awaited direct call', () => {
		const code = lines('async function f() {', '  const item = await create();', '', '  await item.open();', '  use(item);', '}');
		assert.equal(fix(code, rule), lines('async function f() {', '  const item = await create();', '  await item.open();', '  use(item);', '}'));
	});

	it('excludes another receiver, a chained receiver, a property assignment and a destructuring declaration', () => {
		for (const pair of [
			['const item = new Item();', 'other.call();'],
			['const item = new Item();', 'item.child.call();'],
			['this.item = new Item();', 'this.item.call();'],
			['const [item] = create();', 'item.call();'],
			['const item = existing;', 'item.call();'],
		]) {
			const code = lines('prepare();', '', pair[0] as string, '', pair[1] as string);
			assert.equal(fix(code, rule), code);
		}
	});

	it('settles with declaration runs and the strong preset', () => {
		const code = lines(
			'function f() {',
			'  prepare();',
			'',
			'  const item = new Item();',
			'',
			'  item.call();',
			'  item.call();',
			'  finish();',
			'}',
		);

		const rules = { ...readPresetRules('strong', true), ...rule };
		const fixed = fix(code, rules);

		assert.equal(fix(fixed, rules), fixed);
		assert.ok(fixed.includes('  const item = new Item();\n  item.call();'), fixed);
	});
});

describe('keep-initialization-with-calls in a short body', () => {
	it('joins the pair of a two-statement body while keep-two-units-together is off', () => {
		const source = lines('function f() {', ' const client = createClient();', '', ' client.start();', '}');
		assert.equal(stable(source, rule), source.replace('createClient();\n\n', 'createClient();\n'));
	});

	it('reports a silenced gap without a fix, so the eslint-disable comment counts as used', () => {
		const source = lines('/* eslint-disable stanza/keep-initialization-with-calls */', 'const item = new Item();', '', 'item.call1();');

		assert.deepEqual(readSilencedRuleIds(source, rule), ['stanza/keep-initialization-with-calls']);
		assert.equal(fix(source, rule), source);
	});
});

describe('keep-initialization-with-calls beside the rules that remove blank lines', () => {
	const strong = { ...readPresetRules('strong'), ...rule };

	it('reads the gap it keeps as tight, so gather-loose-statements leaves the run below the group in any fix order', () => {
		const source = lines(
			'async function f(path, data) {',
			'  const stream = await open(path);',
			'',
			'  stream.write(data);',
			'',
			'  logger.info("a");',
			'',
			'  emitter.on("x", handler);',
			'}',
		);

		const expected = source.replace('await open(path);\n\n', 'await open(path);\n');

		assert.equal(settled(source, strong), expected);
		assert.equal(fixRule(source, strong, 'gather-loose-statements'), source);
	});

	const loose: [where: string, above: string[], below: string[]][] = [
		['above and below', ['audit();', ''], ['', 'print(2);']],
		['above', ['audit();', ''], []],
		['below', [], ['', 'print(2);']],
		['in pairs above and below', ['audit();', '', 'audit2();', ''], ['', 'print(2);', '', 'print(3);']],
	];

	for (const [where, above, below] of loose) {
		it('joins an initialization with loose statements ' + where + ', which stay apart', () => {
			const source = (gap: string[]) => lines('function f(sid) {', ...above, 'const svc = new Service(sid);', ...gap, 'svc.start();', ...below, '}');
			assert.equal(settled(source(['']), strong), source([]));
		});
	}

	it('joins an initialization whose upper gap a separation keeps out of every loose run', () => {
		const source = lines('function f() {', "  print('test');", '', "  print('more');", '', '  const item = new Item();', '', '  item.call1();', '}');
		assert.equal(settled(source, strong), source.replace('new Item();\n\n', 'new Item();\n'));
	});
});
