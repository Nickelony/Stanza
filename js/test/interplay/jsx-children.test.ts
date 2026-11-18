import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import { Linter } from 'eslint';

import { fix, fixWithPreset, lines, lintWithPreset, readPresetRules, reporters, type Entries } from '../helpers.ts';

describe('separate-multi-line-jsx-children', () => {
	const tightLines = ['const view = (', '  <div>', '    <A />', '    <B>', '      x', '    </B>', '  </div>', ');'];
	const apartLines = ['const view = (', '  <div>', '    <A />', '', '    <B>', '      x', '    </B>', '  </div>', ');'];
	const tight = lines(...tightLines);
	const apart = lines(...apartLines);
	const file = 'file.tsx';

	it('is reported once beside separate-multi-line-constructs, which leaves the children alone and is left alone by it', () => {
		const code = lines('if (a) {', '  foo();', '}', 'bar();', ...tightLines);
		const both: Record<string, Linter.RuleEntry> = { 'separate-multi-line-constructs': 'error', 'separate-multi-line-jsx-children': 'error' };

		assert.deepEqual(reporters(tight, { 'separate-multi-line-constructs': 'error' }, file), []);
		assert.deepEqual(reporters(tight, both, file), ['stanza/separate-multi-line-jsx-children']);
		assert.deepEqual(reporters(code, { 'separate-multi-line-jsx-children': 'error' }, file), ['stanza/separate-multi-line-jsx-children']);

		assert.deepEqual(reporters(code, both, file), [
			'stanza/separate-multi-line-constructs',
			'stanza/separate-multi-line-constructs',
			'stanza/separate-multi-line-jsx-children',
		]);
	});

	it('writes one text in either order of the two rules, and a second fix changes nothing', () => {
		const code = lines('if (a) {', '  foo();', '}', 'bar();', ...tightLines);
		const entries = Object.entries({ 'separate-multi-line-constructs': 'error', 'separate-multi-line-jsx-children': 'error' } as const);
		const expected = lines('if (a) {', '  foo();', '}', '', 'bar();', '', ...apartLines);

		for (const order of [entries, entries.toReversed()]) {
			const rules = Object.fromEntries(order);

			assert.equal(fix(code, rules, {}, file), expected);
			assert.equal(fix(expected, rules, {}, file), expected);
		}
	});

	it('is enabled at the strong level and not below it', () => {
		assert.equal(fixWithPreset(tight, 'strong', file), apart);
		assert.equal(fixWithPreset(tight, 'moderate', file), tight);
	});
});

describe('the JSX child rules', () => {
	const file = 'file.tsx';
	const separateMulti = 'separate-multi-line-jsx-children';
	const keepMulti = 'keep-multi-line-jsx-children-together';
	const keepSingle = 'keep-single-line-jsx-children-together';
	const commented = 'separate-commented-jsx-children';

	const sample = lines(
		'const view = (',
		'  <section>',
		'    <A />',
		'',
		'    <B />',
		'    {/* the body */}',
		'    <C />',
		'',
		'    <D>',
		'      <E />',
		'',
		'      <F />',
		'    </D>',
		'',
		'    {/* the footer */}',
		'    <G />',
		'    <H>',
		'      x',
		'    </H>',
		'  </section>',
		');',
	);

	const strong = lines(
		'const view = (',
		'  <section>',
		'    <A />',
		'    <B />',
		'',
		'    {/* the body */}',
		'    <C />',
		'',
		'    <D>',
		'      <E />',
		'      <F />',
		'    </D>',
		'',
		'    {/* the footer */}',
		'    <G />',
		'',
		'    <H>',
		'      x',
		'    </H>',
		'  </section>',
		');',
	);

	const tight = lines(
		'const view = (',
		'  <section>',
		'    <A />',
		'    <B />',
		'',
		'    {/* the body */}',
		'    <C />',
		'    <D>',
		'      <E />',
		'      <F />',
		'    </D>',
		'',
		'    {/* the footer */}',
		'    <G />',
		'    <H>',
		'      x',
		'    </H>',
		'  </section>',
		');',
	);

	it('write the strong level text: the single-line children tight, the comments and the multi-line children apart', () => {
		assert.equal(fixWithPreset(sample, 'strong', file), strong);
		assert.equal(fixWithPreset(strong, 'strong', file), strong);
		assert.deepEqual(lintWithPreset(strong, 'strong', file), []);
	});

	it('are enabled at the strong level and not below it', () => {
		assert.equal(fixWithPreset(sample, 'moderate', file), sample);
		assert.ok(commented in readPresetRules('strong') && keepSingle in readPresetRules('strong'));
		assert.ok(!(commented in readPresetRules('moderate')) && !(keepSingle in readPresetRules('moderate')));
		assert.ok(!(keepMulti in readPresetRules('strong')) && keepMulti in readPresetRules('strong', true));
	});

	it('hand the multi-line gaps to the tight half while it is enabled, whether it comes with the preset or replaces the separating half', () => {
		const over: Entries = { ...readPresetRules('strong'), [keepMulti]: 'error' };
		const off: Entries = { ...readPresetRules('strong'), [separateMulti]: 'off', [keepMulti]: 'error' };

		assert.equal(fix(sample, over, {}, file), tight);
		assert.equal(fix(sample, off, {}, file), tight);
		assert.equal(fix(tight, over, {}, file), tight);
		assert.deepEqual(reporters(tight, over, file), []);
	});

	it('leave a gap beside a multi-line child to the multi-line rule and a gap beside a comment to the commented rule', () => {
		const code = lines('const view = (', '  <div>', '    <A />', '    {/* note */}', '    <B />', '    <C>', '      x', '    </C>', '  </div>', ');');
		const rules: Entries = { [separateMulti]: 'error', [commented]: 'error', [keepSingle]: 'error' };

		assert.deepEqual(reporters(code, rules, file), [`stanza/${commented}`, `stanza/${separateMulti}`]);
		assert.deepEqual(reporters(code, { [commented]: 'error' }, file), [`stanza/${commented}`]);
		assert.deepEqual(reporters(code, { [separateMulti]: 'error' }, file), [`stanza/${separateMulti}`]);
	});

	it('write one text in either order of the rules, for every subset of them, and a second fix changes nothing', () => {
		const names = [separateMulti, keepMulti, keepSingle, commented, 'separate-multi-line-constructs'];

		for (let mask = 1; mask < 2 ** names.length; mask += 1) {
			const entries = names.filter((_, index) => (mask >> index) % 2 === 1).map((name) => [name, 'error'] as const);
			const label = entries.map(([name]) => name).join(', ');
			const forward = Object.fromEntries(entries);

			for (const code of [sample, strong, tight]) {
				const fixed = fix(code, forward, {}, file);

				assert.equal(fix(code, Object.fromEntries(entries.toReversed()), {}, file), fixed, label);
				assert.equal(fix(fixed, forward, {}, file), fixed, label);
				assert.deepEqual(reporters(fixed, forward, file), [], label);
			}
		}
	});

	it('leave an element with text or a space as written, whichever rule is on', () => {
		const text = lines(
			'const view = (',
			'  <div>',
			'    <A />',
			'',
			'    {/* note */}',
			'    <B>',
			'      x',
			'    </B>',
			'    text',
			'  </div>',
			');',
		);

		const space = lines(
			'const view = (',
			'  <div>',
			'    <A />',
			"    {' '}",
			'    {/* note */}',
			'    <B>',
			'      x',
			'    </B>',
			'  </div>',
			');',
		);

		for (const code of [text, space]) {
			assert.equal(fixWithPreset(code, 'strong', file, true), code);
			assert.equal(fix(code, { [keepMulti]: 'error', [keepSingle]: 'error' }, {}, file), code);
		}
	});
});
