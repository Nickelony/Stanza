import { describe, expect, it } from 'vitest';

import { trusted } from '../../src/util/trusted-html.ts';

describe('the trusted HTML of the catalog', () => {
	it('holds the markup with its links and tokens written back', () => {
		expect(trusted('a <t3>b</t> c')).toEqual({ __html: 'a <span class="t3">b</span> c' });
	});

	it('is the same object for the same markup, so a component sees no change', () => {
		expect(trusted('<t5>same</t>')).toBe(trusted('<t5>same</t>'));
		expect(trusted('<t5>same</t>')).not.toBe(trusted('<t6>same</t>'));
	});
});
