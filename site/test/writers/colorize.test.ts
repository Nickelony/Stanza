import { describe, expect, it } from 'vitest';

import { colorize } from '../../src/writers/colorize.ts';

const kinds = (syntax: 'editorconfig' | 'js' | 'toml', line: string): string[] =>
	colorize(syntax, line)[0]!.tokens.map((token) => `${token.kind ?? 'plain'}:${token.text}`);

describe('the colors of generated text', () => {
	it('mark the comment, the section and the value of an .editorconfig line', () => {
		expect(colorize('editorconfig', '# note')[0]).toEqual({ kind: 'comment', tokens: [{ text: '# note', kind: null }] });
		expect(colorize('editorconfig', '[*.cs]')[0]!.kind).toBe('section');
		expect(kinds('editorconfig', 'key = value')).toEqual(['plain:key', 'plain: = ', 'value:value']);
	});

	it('read a TOML table as it reads an .editorconfig', () => {
		expect(colorize('toml', '[tool.stanza]\nlevel = "strong"\n').map((line) => line.kind ?? line.tokens.at(-1)!.kind)).toEqual(['section', 'value']);
	});

	it('mark the keywords, strings and comments of an eslint.config.mjs, and leave the rest plain', () => {
		expect(kinds('js', "import stanza from 'eslint-plugin-stanza';")).toEqual([
			'section:import',
			'plain: stanza ',
			'section:from',
			'plain: ',
			"value:'eslint-plugin-stanza'",
			'plain:;',
		]);

		expect(kinds('js', "\t'stanza/a': ['warn', { list: ['it\\'s'] }], // note")[1]).toBe("value:'stanza/a'");
		expect(kinds('js', "x: 'it\\'s'")).toContain("value:'it\\'s'");
		expect(kinds('js', '\t// note')).toEqual(['plain:\t', 'comment:// note']);
		expect(kinds('js', '')).toEqual(['plain:']);
	});

	it('keep a block with no colors whole', () => {
		expect(colorize('plain', 'a\nb\n')).toEqual([{ kind: null, tokens: [{ text: 'a\nb\n', kind: null }] }]);
	});
});
