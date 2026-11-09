// @vitest-environment happy-dom
import { afterEach, describe, expect, it } from 'vitest';

import { rowElement } from '../../src/components/row-list/row-element.ts';

afterEach(() => {
	document.body.replaceChildren();
});

describe('the row of a list', () => {
	it('is found by its ID, which no selector has to quote', () => {
		document.body.innerHTML = [
			'<div id="panel">',
			'<ul><li data-id="stanza_max_line_length"></li><li data-id="separate-await-runs.typeImports"></li><li data-id="ZA0001"></li></ul>',
			'</div>',
			'<ul><li data-id="outside"></li></ul>',
		].join('');

		const panel = document.getElementById('panel');

		expect(rowElement(panel, 'separate-await-runs.typeImports')?.dataset.id).toBe('separate-await-runs.typeImports');
		expect(rowElement(panel, 'ZA0001')?.dataset.id).toBe('ZA0001');
	});

	it('is none while the panel lacks it or the panel is missing', () => {
		document.body.innerHTML = '<div id="panel"><ul><li data-id="ZA0001"></li></ul></div><ul><li data-id="outside"></li></ul>';

		expect(rowElement(document.getElementById('panel'), 'outside')).toBeUndefined();
		expect(rowElement(null, 'ZA0001')).toBeUndefined();
	});
});
