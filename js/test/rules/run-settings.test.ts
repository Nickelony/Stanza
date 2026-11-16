import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import { readSharedSettings } from '../../src/analysis/environment.ts';
import { fix, lines, readPresetRules } from '../helpers.ts';

const invocationRules = { 'separate-invocation-runs': 'error', 'separate-await-runs': 'error', 'keep-statement-runs-together': 'error' } as const;

describe('configured receiver runs', () => {
	it('groups direct synchronous and awaited calls on one receiver', () => {
		const code = lines('prepare();', 'this.db.add(cart);', 'await this.db.flush();', 'await this.db.refresh(cart);', 'finish();');
		const expected = lines('prepare();', '', 'this.db.add(cart);', 'await this.db.flush();', 'await this.db.refresh(cart);', '', 'finish();');
		const settings = { stanza: { receiverRoots: ['db'] } };

		assert.equal(fix(code, invocationRules, settings), expected);
		assert.equal(fix(expected, invocationRules, settings), expected);
	});

	it('moves a blank line from inside a configured receiver run to its outer edges', () => {
		const code = lines('start();', 'db.add(cart);', '', 'await db.flush();', 'await db.refresh(cart);', 'finish();');
		const expected = lines('start();', '', 'db.add(cart);', 'await db.flush();', 'await db.refresh(cart);', '', 'finish();');
		const settings = { stanza: { receiverRoots: ['db'] } };

		assert.equal(fix(code, invocationRules, settings), expected);
		assert.equal(fix(expected, invocationRules, settings), expected);
	});

	it('groups nested receiver paths under their first meaningful root', () => {
		const code = lines('prepare();', 'this.db.items.add(cart);', 'await this.db.flush();', 'await this.db.refresh(cart);', 'finish();');
		const settings = { stanza: { receiverRoots: ['db'] } };
		const expected = lines('prepare();', '', 'this.db.items.add(cart);', 'await this.db.flush();', 'await this.db.refresh(cart);', '', 'finish();');

		assert.equal(fix(code, invocationRules, settings), expected);
		assert.equal(fix(expected, invocationRules, settings), expected);
	});

	it('supports wildcard roots while excluding direct this and self calls', () => {
		const code = lines('prepare();', 'cache.items.add(cart);', 'await cache.flush();', 'finish();');

		assert.equal(
			fix(code, invocationRules, { stanza: { receiverRoots: ['*'] } }),
			lines('prepare();', '', 'cache.items.add(cart);', 'await cache.flush();', '', 'finish();'),
		);

		for (const name of ['this', 'self']) {
			const direct = lines(`${name}.add(cart);`, `await ${name}.flush();`, `await ${name}.refresh(cart);`);

			assert.equal(
				fix(direct, invocationRules, { stanza: { receiverRoots: ['*'] } }),
				lines(`${name}.add(cart);`, '', `await ${name}.flush();`, `await ${name}.refresh(cart);`),
			);
		}
	});

	it('keeps different meaningful roots apart', () => {
		const code = lines('this.db.add(cart);', 'await this.db.flush();', 'this.cache.add(cart);', 'await this.cache.flush();');

		assert.equal(
			fix(code, invocationRules, { stanza: { receiverRoots: ['*'] } }),
			lines('this.db.add(cart);', 'await this.db.flush();', '', 'this.cache.add(cart);', 'await this.cache.flush();'),
		);
	});

	it('keeps the existing await grouping with no receiver roots', () => {
		const code = lines('this.db.add(cart);', 'await this.db.flush();', 'await this.db.refresh(cart);');
		assert.equal(fix(code, invocationRules), lines('this.db.add(cart);', '', 'await this.db.flush();', 'await this.db.refresh(cart);'));
	});

	it('does not join an unconfigured receiver or different structural receivers', () => {
		const code = lines('other.db.add(cart);', 'this.db.add(cart);', 'await this.db.flush();', 'finish();');

		assert.equal(
			fix(code, invocationRules, { stanza: { receiverRoots: ['db'] } }),
			lines('other.db.add(cart);', '', 'this.db.add(cart);', 'await this.db.flush();', '', 'finish();'),
		);

		const unconfigured = lines('logger.add(cart);', 'await logger.flush();', 'await logger.refresh(cart);');

		assert.equal(
			fix(unconfigured, invocationRules, { stanza: { receiverRoots: ['db'] } }),
			lines('logger.add(cart);', '', 'await logger.flush();', 'await logger.refresh(cart);'),
		);
	});

	it('keeps multi-line calls and comments out of a receiver run', () => {
		const code = lines('db.add(cart);', 'await db.flush(', '  options,', ');', '// refresh', 'await db.refresh(cart);', 'finish();');
		const fixed = fix(code, { ...readPresetRules('strong'), ...invocationRules }, { stanza: { receiverRoots: ['db'] } });

		assert.ok(fixed.includes('db.add(cart);\n\nawait db.flush('), fixed);
		assert.ok(fixed.includes(');\n\n// refresh'), fixed);
		assert.equal(fix(fixed, { ...readPresetRules('strong'), ...invocationRules }, { stanza: { receiverRoots: ['db'] } }), fixed);
	});

	it('retains assertion and test root precedence', () => {
		const code = lines('prepare();', 'assert.ok(a);', 'assert.equal(b, 1);', 'finish();');
		const rules = { ...invocationRules, 'separate-assertion-runs': 'error' } as const;

		assert.equal(fix(code, rules, { stanza: { receiverRoots: ['assert'] } }), fix(code, rules));
	});
});

describe('declaration shapes', () => {
	const rules = { 'separate-declaration-runs': 'error', 'keep-statement-runs-together': 'error' } as const;
	const settings = { stanza: { declarationShapes: 'split' } };

	it('splits array destructuring runs from ordinary declarations', () => {
		const code = lines('const [value, setValue] = useState();', 'const [other, setOther] = useOther();', 'const count = 12;', 'let name = "test";');

		const expected = lines(
			'const [value, setValue] = useState();',
			'const [other, setOther] = useOther();',
			'',
			'const count = 12;',
			'let name = "test";',
		);

		assert.equal(fix(code, rules, settings), expected);
		assert.equal(fix(expected, rules, settings), expected);
	});

	it('merges declaration shapes by default', () => {
		const code = lines('const [value] = read();', 'const count = 12;', 'const name = "test";');
		assert.equal(fix(code, rules), code);
	});

	it('gives object patterns their own shape', () => {
		const code = lines('const { a } = read();', 'const { b } = read();', 'const [c] = read();', 'const [d] = read();');

		assert.equal(
			fix(code, rules, settings),
			lines('const { a } = read();', 'const { b } = read();', '', 'const [c] = read();', 'const [d] = read();'),
		);
	});

	it('does not form a run from mixed-shape declarations', () => {
		const code = lines('const [a] = read(), b = 1;', 'const [c] = read(), d = 2;', 'finish();');
		assert.equal(fix(code, rules, settings), code);
	});

	it('rejects an unsupported declaration shape setting', () => {
		assert.throws(() => readSharedSettings({ stanza: { declarationShapes: 'hooks' } }), /must be "merge" or "split"/);
	});
});

describe('receiver roots without a receiver rule', () => {
	const selected = { stanza: { receiverRoots: ['*'] } };
	const invocations = { 'separate-invocation-runs': 'error' } as const;

	it('separates an await run as it would without receiver roots when no rule reads receivers', () => {
		const source = lines('async function f() {', ' prepare();', ' await db.flush();', ' await db.refresh();', ' done();', '}');
		const expected = lines('async function f() {', ' prepare();', '', ' await db.flush();', ' await db.refresh();', '', ' done();', '}');

		assert.equal(fix(source, { 'separate-await-runs': 'error' }, selected), expected);
	});

	it('keeps the blank line between two awaits that gather-loose-statements leaves in a short run', () => {
		const source = lines('async function f() {', ' prepare();', ' await db.flush();', '', ' await db.refresh();', ' done();', '}');
		const expected = lines('async function f() {', ' prepare();', '', ' await db.flush();', '', ' await db.refresh();', '', ' done();', '}');

		assert.equal(fix(source, { 'separate-await-runs': 'error', 'gather-loose-statements': 'error' }, selected), expected);
	});

	it('joins two awaits of one run with keep-statement-runs-together', () => {
		const source = lines('async function f() {', ' prepare();', ' await db.flush();', '', ' await db.refresh();', ' done();', '}');
		const expected = lines('async function f() {', ' prepare();', '', ' await db.flush();', ' await db.refresh();', '', ' done();', '}');

		assert.equal(fix(source, { 'separate-await-runs': 'error', 'keep-statement-runs-together': 'error' }, selected), expected);
	});

	it('strips only the leading this, so a member named self is the root of the calls through it', () => {
		const source = lines('this.self.db.add();', 'this.self.db.save();', 'this.self.cache.clear();');
		assert.equal(fix(source, invocations, selected), source);
	});

	it('separates the calls of two receiver roots after this', () => {
		const source = lines('this.db.add();', 'this.db.save();', 'this.cache.clear();');
		assert.equal(fix(source, invocations, selected), lines('this.db.add();', 'this.db.save();', '', 'this.cache.clear();'));
	});

	it('reads no receiver through optional access', () => {
		const source = lines('prepare();', 'db?.add();', 'db?.save();', 'done();');
		assert.equal(fix(source, invocations, selected), source);
	});

	it('reads the receiver through a non-null assertion', () => {
		const source = lines('prepare();', 'db!.add();', 'db!.save();', 'done();');
		assert.equal(fix(source, invocations, selected), lines('prepare();', '', 'db!.add();', 'db!.save();', '', 'done();'));
	});
});
