import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import { Linter } from 'eslint';

import { createConfig, fix, fixOneByOne, fixRule, lines, readPresetRules, readSuppressedMessages } from '../helpers.ts';

describe('the keep rules under a directive', () => {
	/** Fixes a source under the linter options ESLint gives by default, which report an unused directive and remove it, checking that one pass settles it in any fix order. */
	function settleUnderDefaults(source: string, rules: Record<string, Linter.RuleEntry>, settings: Record<string, unknown>): string {
		const config = createConfig(rules, settings).map((entry) => ({ ...entry, linterOptions: {} }));
		const linter = new Linter();
		const options = { filename: 'file.ts', stats: true };
		const result = linter.verifyAndFix(source, config, options);

		assert.deepEqual(result.messages, []);
		assert.ok(linter.getFixPassCount() <= 1, 'needed ' + linter.getFixPassCount() + ' fix passes');
		assert.equal(fixOneByOne(source, config, 'first'), result.output);
		assert.equal(fixOneByOne(source, config, 'last'), result.output);

		linter.verify(source, config, { filename: 'file.ts' });

		// Each sample silences one keeper, so every report a directive silences is that keeper's.
		const silenced = readSuppressedMessages(linter);

		assert.ok(silenced.length > 0, 'no report is silenced');
		assert.ok(silenced.every((message) => message.fix === undefined));

		return result.output;
	}

	/** For each keep rule, the units above and below a gap it keeps, the settings it needs, and what sets the sample apart from another of the rule. */
	const samples: [rule: string, above: string[], below: string[], settings?: Record<string, unknown>, label?: string][] = [
		['keep-guard-clauses-together', ['run(a);', 'if (!a) return;'], ['if (!b) return;', 'go(a, b);']],
		['keep-guard-with-binding', ['audit(id);', 'const order = find(id);'], ['if (!order) return null;', 'send(order);', 'return order;']],
		['keep-accumulator-with-loop', ['check(items);', 'let total = 0;'], ['for (const item of items) {', '  total += item;', '}', 'use(total);']],
		['keep-jump-with-binding', ['log(a);', '', 'const b = load(a);'], ['return b;']],

		[
			'keep-jump-with-binding',
			['check(a);', 'const c = load(a);', '', 'const d = load(c);', '', 'const e = load(d);'],
			['return e;'],
			{ stanza: { maxLooseRunSize: 2 } },
			' over a binding a loose run then takes',
		],

		['keep-assertion-with-binding', ['audit(id);', 'const order = load(id);'], ['assert(order);', 'ship(order);']],
		['keep-initialization-with-calls', ['audit();', '', 'const svc = new Service(id);'], ['svc.start();', 'svc.stop();']],
		['keep-receiver-groups-together', ['prepare();', 'db.add(a);'], ['db.save();', 'done();'], { stanza: { receiverRoots: ['db'] } }],
		['keep-dependent-bindings-together', ['run();', 'const first = load();'], ['const second = build(first);', 'use(second);']],
	];

	for (const [rule, above, below, settings = {}, label = ''] of samples) {
		const directives: [form: string, body: string[]][] = [
			['eslint-disable-next-line above the unit below', [...above, '', '// eslint-disable-next-line stanza/' + rule, ...below]],
			['eslint-disable-line after the unit above', [...above.slice(0, -1), above.at(-1) + ' // eslint-disable-line stanza/' + rule, '', ...below]],
			['eslint-disable over the file', ['/* eslint-disable stanza/' + rule + ' */', ...above, '', ...below]],
		];

		for (const [form, body] of directives) {
			it(rule + label + ' silenced by ' + form + ' writes the text it writes when off, and the directive stays', () => {
				const source = lines('function f(id, items, a, b) {', ...body, '}');
				const strong = readPresetRules('strong');

				assert.equal(settleUnderDefaults(source, { ...strong, [rule]: 'error' }, settings), fix(source, strong, settings));
			});
		}
	}

	for (const [rule, settings, open, close, apart] of [
		[
			'keep-receiver-groups-together',
			{ stanza: { receiverRoots: ['db'] } },
			['db.add(a);', 'db.flush();'],
			['db.count += 1;', 'db.count += 2;'],
			'prepare();',
		],

		[
			'keep-dependent-bindings-together',
			{},
			['const a1 = load();', 'const a2 = build(a1);'],
			['const a3 = build(a2);', 'const a4 = build(a3);'],
			'run();',
		],
	] as const) {
		it(rule + ' keeps a directive whose gap the fix of the rest of the group makes a section boundary', () => {
			const directive = '// eslint-disable-next-line stanza/' + rule;
			const source = lines('function f(a) {', apart, '', open[0], '', open[1], '', directive, close[0], '', close[1], 'done();', '}');
			const expected = lines('function f(a) {', apart, '', open[0], open[1], '', directive, close[0], close[1], '', 'done();', '}');

			assert.equal(settleUnderDefaults(source, { ...readPresetRules('strong'), [rule]: 'error' }, settings), expected);
		});
	}

	it('keep-statement-runs-together silenced beside gather-loose-statements keeps the blank line and the directive in every fix order', () => {
		const rules = { 'keep-statement-runs-together': 'error', 'gather-loose-statements': 'error', 'separate-declaration-runs': 'error' } as const;

		const source = lines(
			'function f() {',
			'  const a = 1;',
			'',
			'  const b = 2;',
			'',
			'  // eslint-disable-next-line stanza/keep-statement-runs-together',
			'  const c = 3;',
			'',
			'  const d = 4;',
			'',
			'  const e = 5;',
			'}',
		);

		const expected = lines(
			'function f() {',
			'  const a = 1;',
			'  const b = 2;',
			'',
			'  // eslint-disable-next-line stanza/keep-statement-runs-together',
			'  const c = 3;',
			'  const d = 4;',
			'  const e = 5;',
			'}',
		);

		assert.equal(settleUnderDefaults(source, rules, {}), expected);
		assert.equal(fixRule(source, rules, 'gather-loose-statements'), source);
	});
});
