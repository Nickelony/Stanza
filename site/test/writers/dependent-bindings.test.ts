import { describe, expect, it } from 'vitest';

import { emptyConfig } from '../../src/state/config.ts';
import { generateEditorConfig } from '../../src/writers/editorconfig.ts';
import { generateEslintConfig } from '../../src/writers/eslint-config.ts';
import { generatePyproject } from '../../src/writers/pyproject.ts';
import { realCatalog, realJavascript, realPython } from '../fixtures.ts';

describe('dependent binding configuration', () => {
	it('exports the C# keeper independently of global run keeping and receiver selection', () => {
		const config = { ...emptyConfig(), rules: { ZA0064: 'warning' as const } };
		const text = generateEditorConfig(realCatalog, config);

		expect(text).toContain('dotnet_diagnostic.ZA0064.severity = warning');
		expect(text).not.toContain('dotnet_diagnostic.ZA0054.severity =');
		expect(text).not.toContain('stanza_receiver_roots =');
		expect(realCatalog.rules.find((rule) => rule.id === 'ZA0064')?.opposite).toBeUndefined();
	});

	it('exports the JavaScript keeper without enabling receiver or global keeping policies', () => {
		const config = { ...emptyConfig('error'), rules: { 'keep-dependent-bindings-together': 'error' as const } };
		const text = generateEslintConfig(realJavascript, config);

		expect(text).toContain("'stanza/keep-dependent-bindings-together': 'error'");
		expect(text).not.toContain("'stanza/keep-statement-runs-together'");
		expect(text).not.toContain('receiverRoots:');
		expect(realJavascript.rules.find((rule) => rule.id === 'keep-dependent-bindings-together')?.opposite).toBeUndefined();
	});

	it('exports the Python keeper as an opt-in code with no required extra settings', () => {
		const config = { ...emptyConfig('error'), rules: { ZA3064: 'error' as const } };
		const text = generatePyproject(realPython, config);

		expect(text).toBe('[tool.stanza]\nenable = ["ZA3064"]\n');
		expect(realPython.rules.find((rule) => rule.id === 'ZA3064')?.opposite).toBeUndefined();
	});
});
