import { describe, expect, it } from 'vitest';

import { emptyConfig } from '../../src/state/config.ts';
import { generatePropertyGroup } from '../../src/writers/msbuild.ts';
import { config } from '../fixtures.ts';

describe('the MSBuild properties', () => {
	it('write none while every property is the default', () => {
		expect(generatePropertyGroup(emptyConfig())).toBe('');
	});

	it('leave out each property at its default and write the others', () => {
		expect(generatePropertyGroup(config({ level: 'strong' }))).toBe('<PropertyGroup>\n\t<StanzaLevel>strong</StanzaLevel>\n</PropertyGroup>');

		expect(generatePropertyGroup(config({ severity: 'warning' }))).toBe(
			'<PropertyGroup>\n\t<StanzaSeverity>warning</StanzaSeverity>\n</PropertyGroup>',
		);

		expect(generatePropertyGroup(config({ formatter: 'csharpier' }))).toBe(
			'<PropertyGroup>\n\t<StanzaFormatter>csharpier</StanzaFormatter>\n</PropertyGroup>',
		);

		expect(generatePropertyGroup(config({ level: 'strong', severity: 'error', formatter: 'csharpier' }))).toBe(
			[
				'<PropertyGroup>',
				'\t<StanzaLevel>strong</StanzaLevel>',
				'\t<StanzaSeverity>error</StanzaSeverity>',
				'\t<StanzaFormatter>csharpier</StanzaFormatter>',
				'</PropertyGroup>',
			].join('\n'),
		);
	});

	it('leave out the severity at the none level, which enables no rule for it to apply to', () => {
		expect(generatePropertyGroup(config({ level: 'none', severity: 'error', formatter: 'csharpier' }))).toBe(
			['<PropertyGroup>', '\t<StanzaLevel>none</StanzaLevel>', '\t<StanzaFormatter>csharpier</StanzaFormatter>', '</PropertyGroup>'].join('\n'),
		);

		expect(generatePropertyGroup(config({ level: 'none', severity: 'warning' }))).toBe(
			'<PropertyGroup>\n\t<StanzaLevel>none</StanzaLevel>\n</PropertyGroup>',
		);
	});
});
