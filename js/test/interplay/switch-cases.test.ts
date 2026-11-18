import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import { Linter } from 'eslint';

import { createConfig, fix, fixWithPreset, lines, readPresetRules, readSuppressedMessages, reporters, settled, type Entries } from '../helpers.ts';

describe('the switch case rules', () => {
	const mixed = lines(
		'switch (x) {',
		'  case 1: return a;',
		'',
		'  case 2: return b;',
		'  case 3:',
		'    foo();',
		'    break;',
		'',
		'  case 4:',
		'    bar();',
		'}',
	);

	const separate = 'separate-multi-line-switch-cases';
	const keepMulti = 'keep-multi-line-switch-cases-together';
	const keepSingle = 'keep-single-line-switch-cases-together';

	const apart = lines(
		'switch (x) {',
		'  case 1: return a;',
		'  case 2: return b;',
		'',
		'  case 3:',
		'    foo();',
		'    break;',
		'',
		'  case 4:',
		'    bar();',
		'}',
	);

	const tight = lines(
		'switch (x) {',
		'  case 1: return a;',
		'  case 2: return b;',
		'  case 3:',
		'    foo();',
		'    break;',
		'  case 4:',
		'    bar();',
		'}',
	);

	it('leave each gap to one rule: the separating half and the single-line rule split the cases between them', () => {
		const rules: Entries = { [separate]: 'error', [keepSingle]: 'error' };

		assert.equal(fix(mixed, rules), apart);
		assert.equal(fix(apart, rules), apart);
		assert.deepEqual(reporters(mixed, rules), [`stanza/${keepSingle}`, `stanza/${separate}`]);
	});

	it('hand the gaps of the tight half to it while it is enabled, so the separating half asks for none of them', () => {
		const both: Entries = { [separate]: 'error', [keepMulti]: 'error' };

		assert.deepEqual(reporters(apart, { [separate]: 'error' }), []);
		assert.deepEqual(reporters(tight, { [separate]: 'error' }), [`stanza/${separate}`, `stanza/${separate}`]);
		assert.deepEqual(reporters(tight, both), []);
		assert.deepEqual(reporters(apart, both), [`stanza/${keepMulti}`, `stanza/${keepMulti}`]);

		assert.equal(
			fix(apart, both),
			lines('switch (x) {', '  case 1: return a;', '  case 2: return b;', '  case 3:', '    foo();', '    break;', '  case 4:', '    bar();', '}'),
		);

		assert.equal(fix(apart, { [keepMulti]: 'error' }), fix(apart, both));
	});

	it('write one text in either order of the three rules, for every subset of them', () => {
		const names = [separate, keepMulti, keepSingle, 'separate-multi-line-constructs'];

		for (let mask = 1; mask < 2 ** names.length; mask += 1) {
			const entries = names.filter((_, index) => (mask >> index) % 2 === 1).map((name) => [name, 'error'] as const);
			const label = entries.map(([name]) => name).join(', ');
			const forward = Object.fromEntries(entries);

			for (const code of [mixed, apart, tight]) {
				const fixed = fix(code, forward);

				assert.equal(fix(code, Object.fromEntries(entries.toReversed())), fixed, label);
				assert.equal(fix(fixed, forward), fixed, label);
				assert.deepEqual(reporters(fixed, forward), [], label);
			}
		}
	});

	it('is judged beside separate-multi-line-constructs, which leaves the cases and judges the statements inside them', () => {
		const code = lines(
			'switch (x) {',
			'  case 1:',
			'    foo();',
			'    if (a) {',
			'      bar();',
			'    }',
			'    break;',
			'  case 2:',
			'    baz();',
			'}',
		);

		const rules: Entries = { 'separate-multi-line-constructs': 'error', [separate]: 'error' };

		assert.deepEqual(reporters(code, rules), [
			'stanza/separate-multi-line-constructs',
			'stanza/separate-multi-line-constructs',
			`stanza/${separate}`,
		]);

		assert.deepEqual(reporters(code, { 'separate-multi-line-constructs': 'error' }), [
			'stanza/separate-multi-line-constructs',
			'stanza/separate-multi-line-constructs',
		]);
	});

	it('enable the separating half at the moderate level, the single-line rule at the minimal level and the tight half in no preset', () => {
		assert.ok(!(separate in readPresetRules('minimal')));
		assert.ok(separate in readPresetRules('moderate'));
		assert.ok(keepSingle in readPresetRules('minimal'));
		assert.ok(!(keepMulti in readPresetRules('strong')));
		assert.ok(keepMulti in readPresetRules('strong', true));
		assert.equal(fixWithPreset(tight, 'minimal'), tight);

		assert.equal(
			fixWithPreset(mixed, 'minimal'),

			lines(
				'switch (x) {',
				'  case 1: return a;',
				'  case 2: return b;',
				'  case 3:',
				'    foo();',
				'    break;',
				'',
				'  case 4:',
				'    bar();',
				'}',
			),
		);

		assert.equal(fixWithPreset(mixed, 'moderate'), apart);
		assert.equal(fixWithPreset(tight, 'strong'), apart);
		assert.equal(fixWithPreset(apart, 'strong', 'file.ts', true), tight);
	});

	it('turn the separating half into the tight half with one more entry over the preset, or with the half off', () => {
		const over: Entries = { ...readPresetRules('moderate'), [keepMulti]: 'error' };
		const off: Entries = { ...readPresetRules('moderate'), [separate]: 'off', [keepMulti]: 'error' };

		assert.equal(fix(apart, over), tight);
		assert.equal(fix(apart, off), tight);
		assert.equal(fix(tight, over), tight);
	});
});

describe('the stacked case labels', () => {
	const rule = 'keep-stacked-case-labels-together';
	const strong = readPresetRules('strong');
	const withBody = lines('switch (x) {', '  case 1:', '', '  case 2:', '    foo();', '    break;', '', '  default:', '    bar();', '}');
	const stacked = lines('switch (x) {', '  case 1:', '  case 2:', '    foo();', '    break;', '', '  default:', '    bar();', '}');

	it('is enabled from the minimal level up', () => {
		for (const level of ['minimal', 'moderate', 'strong'] as const) {
			assert.equal(fixWithPreset(withBody, level), stacked, level);
		}

		assert.equal(fixWithPreset(withBody, 'none'), withBody);
	});

	it('writes one text in one pass in every order of the rules, with the case rules and the rest of the strong level on', () => {
		const sources = [
			withBody,

			lines(
				'switch (x) {',
				'  case 1:',
				'',
				'  case 2:',
				'',
				'  case 3: return a;',
				'',
				'  default:',
				'',
				'  case 4:',
				'    bar();',
				'    break;',
				'}',
			),

			lines('switch (x) {', '  case 1:', '', '  case 2:', '  // note', '', '  case 3:', '    foo();', '}'),
			lines('switch (x) {', '  case 1:', '', '  case 2:', '    foo();', '    foo();', '', '  case 3:', '', '  case 4:', '    bar();', '}'),
		];

		for (const source of sources) {
			const entries = Object.entries({
				...strong,
				'keep-multi-line-switch-cases-together': 'error',
				'keep-statement-runs-together': 'error',
			} as Record<string, Linter.RuleEntry>);

			assert.equal(settled(source, Object.fromEntries(entries)), settled(source, Object.fromEntries(entries.toReversed())));
			assert.equal(settled(source, strong), fix(source, strong));
		}
	});

	it('shares no gap with the case rules, whichever of them is enabled beside it', () => {
		const code = lines('switch (x) {', '  case 1:', '', '  case 2:', '    foo();', '    bar();', '', '  case 3:', '    baz();', '    qux();', '}');
		const keepMulti = 'keep-multi-line-switch-cases-together';

		assert.deepEqual(reporters(code, { [rule]: 'error', 'separate-multi-line-switch-cases': 'error' }), [`stanza/${rule}`]);
		assert.deepEqual(reporters(code, { [rule]: 'error', [keepMulti]: 'error' }), [`stanza/${rule}`, `stanza/${keepMulti}`]);
		assert.deepEqual(reporters(code, { [rule]: 'error', 'keep-single-line-switch-cases-together': 'error' }), [`stanza/${rule}`]);
		assert.deepEqual(reporters(code, { [rule]: 'error', 'keep-two-units-together': 'error' }), [`stanza/${rule}`]);
	});

	it('leaves the blank line Prettier keeps below a label, whatever the rules write', () => {
		const code = lines('switch (x) {', '  case 1:', '', '    foo();', '    break;', '', '  case 2:', '    bar();', '}');
		assert.equal(fix(code, { ...strong, [rule]: 'error' }), code);
	});

	it('is silenced by an eslint-disable comment, which leaves the gap as written', () => {
		const rules: Entries = { [rule]: 'error' };
		const next = lines('switch (x) {', '  case 1:', `  // eslint-disable-next-line stanza/${rule}`, '', '  case 2:', '    foo();', '}');
		const everywhere = lines(`/* eslint-disable stanza/${rule} */`, 'switch (x) {', '  case 1:', '', '  case 2:', '    foo();', '}');
		const trailing = lines('switch (x) {', `  case 1: // eslint-disable-line stanza/${rule}`, '', '  case 2:', '    foo();', '}');

		for (const source of [next, everywhere, trailing]) {
			assert.equal(fix(source, rules), source);
			assert.deepEqual(new Linter().verify(source, createConfig(rules), { filename: 'file.ts' }), []);
		}

		const linter = new Linter();

		linter.verify(everywhere, createConfig(rules), { filename: 'file.ts' });

		assert.deepEqual(
			readSuppressedMessages(linter).map((message) => message.ruleId),
			[`stanza/${rule}`],
		);
	});
});
