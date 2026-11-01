import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import type { Rule } from 'eslint';

import { createRule } from '../src/createRule.ts';

type Options = [{ readonly flat: boolean; readonly nested: { readonly left: number; readonly right: number } }];

const defaults: Options = [{ flat: true, nested: { left: 1, right: 2 } }];

/** Creates a rule that hands the options it receives to `received`, and reads them from the context it is given. */
function readOptions(given: unknown[]): Readonly<Options> {
	let received: Readonly<Options> | undefined;

	const rule = createRule<Options, 'message'>({
		name: 'example',
		meta: { type: 'layout', docs: { description: 'Example.' }, messages: { message: 'Message.' }, schema: [], defaultOptions: defaults },

		create(_context, options) {
			received = options;
			return {};
		},
	});

	rule.create({ options: given } as unknown as Rule.RuleContext);

	assert.ok(received);

	return received;
}

describe('createRule', () => {
	it('links the page of the rule in its docs', () => {
		const rule = createRule<[], 'message'>({
			name: 'example',
			meta: { type: 'layout', docs: { description: 'Example.' }, messages: { message: 'Message.' }, schema: [] },
			create: () => ({}),
		});

		assert.equal(rule.meta.docs?.url, 'https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/example.md');
		assert.equal(rule.meta.docs?.description, 'Example.');
	});

	it('gives the defaults when the user gives no options', () => {
		assert.deepEqual(readOptions([]), defaults);
	});

	it('merges the option of the user into the default one, key by key and level by level', () => {
		assert.deepEqual(readOptions([{ nested: { right: 5 } }]), [{ flat: true, nested: { left: 1, right: 5 } }]);
		assert.deepEqual(readOptions([{ flat: false }]), [{ flat: false, nested: { left: 1, right: 2 } }]);
	});

	it('drops an option that has no default and leaves the defaults as they were', () => {
		assert.deepEqual(readOptions([{ flat: false }, { extra: true }]), [{ flat: false, nested: { left: 1, right: 2 } }]);
		assert.deepEqual(defaults, [{ flat: true, nested: { left: 1, right: 2 } }]);
	});
});
