// @vitest-environment happy-dom
import { cleanup, render } from '@testing-library/preact';
import { afterEach, describe, expect, it } from 'vitest';

import { RowInput } from '../../src/components/row-list/parts.ts';

afterEach(cleanup);

/** Reads the CSS Emotion wrote for the class of the only text box on the page, one rule to a line. */
function readRulesOf(element: HTMLElement | undefined): string[] {
	const css = [...document.querySelectorAll('style[data-emotion]')].map((style) => style.textContent).join('\n');
	return css.split('\n').filter((rule) => rule.includes(`.${element?.className.split(' ')[0] ?? ''}`));
}

describe('a text box of the option list', () => {
	it('draws the changed border under forced colors in the invalid state too, so the rule for it stands after the invalid border', () => {
		render(<RowInput $changed aria-invalid="true" />);

		const rules = readRulesOf(document.getElementsByTagName('input')[0]);
		const invalid = rules.findIndex((rule) => rule.includes("[aria-invalid='true']{border-color:var(--del-fg)"));
		const forced = rules.findIndex((rule) => rule.startsWith('@media (forced-colors: active)') && rule.includes("[aria-invalid='true']"));

		expect(invalid).toBeGreaterThanOrEqual(0);
		expect(forced).toBeGreaterThan(invalid);
		expect(rules[forced]).toContain('border-color:Highlight');

		// The same selector as the invalid rule, so the later rule wins by order and the forced palette keeps the changed border.
		const className = document.getElementsByTagName('input')[0]!.className.split(' ')[0];

		expect(rules[forced]).toContain(`.${className}[aria-invalid='true']{border-width:2px;border-color:Highlight;}`);
	});

	it('draws no changed border when it is unchanged', () => {
		render(<RowInput $changed={false} />);
		expect(readRulesOf(document.getElementsByTagName('input')[0]).some((rule) => rule.includes('Highlight'))).toBe(false);
	});
});
