import { describe, expect, it } from 'vitest';

import { emptyConfig } from '../../src/state/config.ts';
import { generateEslintConfig } from '../../src/writers/eslint-config.ts';
import { realJavascript } from '../fixtures.ts';

describe('shared receiver settings in JavaScript output', () => {
	it('quotes word-valued comparisons and declaration shapes as valid JavaScript', () => {
		const config = { ...emptyConfig('error'), options: { receiverRoots: 'db', receiverComparison: 'path', declarationShapes: 'split' } };
		const text = generateEslintConfig(realJavascript, config);

		expect(text).toContain("receiverRoots: ['db'],");
		expect(text).toContain("receiverComparison: 'path',");
		expect(text).toContain("declarationShapes: 'split',");

		const evaluate = new Function('prettier', 'stanza', text.replace(/^import .*;$/gm, '').replace('export default ', 'return '));
		const exported = evaluate({}, { configs: { moderate: {} } });

		expect(exported[2].settings.stanza).toMatchObject({ receiverComparison: 'path', declarationShapes: 'split' });
	});
});
