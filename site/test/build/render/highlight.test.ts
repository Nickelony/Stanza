import { readFileSync } from 'node:fs';
import { join } from 'node:path';

import { describe, expect, it } from 'vitest';

import { highlightCatalog } from '../../../build/render/highlight.ts';
import { realCatalog } from '../../fixtures.ts';

/** WCAG AA for text below 18px. */
const minimumContrast = 4.5;
const stylesheet = readFileSync(join(import.meta.dirname, '..', '..', '..', 'src', 'style.css'), 'utf8');

/** A custom property of the `:root` block that opens with the selector, as `style.css` sets it. */
function themeColor(selector: string, property: string): string {
	const block = stylesheet.slice(stylesheet.indexOf(`${selector} {`));
	const match = new RegExp(`${property}:\\s*(#[0-9a-f]{6})`, 'i').exec(block.slice(0, block.indexOf('}')));

	expect(match, `${selector} sets ${property}`).not.toBeNull();

	return match![1]!;
}

type Rgb = [number, number, number];

function parseHex(color: string): Rgb {
	return [1, 3, 5].map((at) => Number.parseInt(color.slice(at, at + 2), 16)) as Rgb;
}

/** A custom property of the block that opens with the selector, which holds a color with an alpha, as `style.css` writes the tint of a diff line. */
function themeTint(selector: string, property: string): { color: Rgb; alpha: number } {
	const block = stylesheet.slice(stylesheet.indexOf(`${selector} {`));
	const match = new RegExp(`${property}:\\s*rgba\\((\\d+),\\s*(\\d+),\\s*(\\d+),\\s*([\\d.]+)\\)`).exec(block.slice(0, block.indexOf('}')));

	expect(match, `${selector} sets ${property}`).not.toBeNull();

	return { color: [Number(match![1]), Number(match![2]), Number(match![3])], alpha: Number(match![4]) };
}

/** The color a tint of this alpha gives over a background, which is what a diff line shows over the code background. */
function over(tint: { color: Rgb; alpha: number }, background: string): string {
	const base = parseHex(background);
	const channels = tint.color.map((channel, index) => Math.round(channel * tint.alpha + base[index]! * (1 - tint.alpha)));

	return `#${channels.map((channel) => channel.toString(16).padStart(2, '0')).join('')}`;
}

function luminance(color: string): number {
	const [red, green, blue] = parseHex(color).map((value) => {
		const channel = value / 255;
		return channel <= 0.03928 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4;
	});

	return 0.2126 * red! + 0.7152 * green! + 0.0722 * blue!;
}

function contrast(foreground: string, background: string): number {
	const [lighter, darker] = [luminance(foreground), luminance(background)].sort((first, second) => second - first);
	return (lighter! + 0.05) / (darker! + 0.05);
}

describe('code colors', () => {
	const themes = [
		{ name: 'light', selector: ':root', rule: /^\.t\d+\{color:(#[0-9a-f]{6})\}$/gim },
		{ name: 'dark', selector: ":root[data-theme='dark']", rule: /^:root\[data-theme="dark"\] \.t\d+\{color:(#[0-9a-f]{6})\}$/gim },
	];

	it('reach WCAG AA on the code background of their theme', async () => {
		const { css } = await highlightCatalog(realCatalog);

		for (const theme of themes) {
			const background = themeColor(theme.selector, '--code-bg');
			const colors = [...css.matchAll(theme.rule)].map((match) => match[1]!);

			expect(colors.length, `${theme.name} token colors`).toBeGreaterThan(0);

			// The plain text of a snippet has no class, so the page's code color carries it.
			for (const color of [...colors, themeColor(theme.selector, '--code-fg')]) {
				expect(contrast(color, background), `${color} on the ${theme.name} code background ${background}`).toBeGreaterThanOrEqual(minimumContrast);
			}
		}
	}, 60_000);

	it('reach WCAG AA on the tinted background of an added and a removed line', async () => {
		const { css } = await highlightCatalog(realCatalog);

		for (const theme of themes) {
			const codeBackground = themeColor(theme.selector, '--code-bg');
			const colors = [...css.matchAll(theme.rule)].map((match) => match[1]!);

			for (const [line, gutter] of [
				['--add-bg', '--add-fg'],
				['--del-bg', '--del-fg'],
			] as const) {
				const background = over(themeTint(theme.selector, line), codeBackground);

				// The text of the line, its gutter mark, and the note a blank line shows in the muted color of the page.
				const texts = [...colors, themeColor(theme.selector, '--code-fg'), themeColor(theme.selector, gutter), themeColor(theme.selector, '--muted')];

				for (const color of texts) {
					expect(contrast(color, background), `${color} on the ${theme.name} ${line} line ${background}`).toBeGreaterThanOrEqual(minimumContrast);
				}
			}
		}
	}, 60_000);
});
