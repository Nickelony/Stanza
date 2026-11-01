import { defaultFormatter, defaultLevel, defaultPackageSeverity, type Formatter, type Level, type PackageSeverity } from '../state/levels.ts';

/** What the MSBuild properties block shows while every property is at its default, so a project sets none. */
export const noPropertiesNeeded = '<!-- No property is needed: the package applies the Moderate level at suggestion with stanza-format. -->';

/**
 * The properties that differ from the default, as the `PropertyGroup` a project file or `Directory.Build.props` holds; empty when none does.
 * The none level enables no rule, so it has no severity to write.
 */
export function generatePropertyGroup(picks: { level: Level; formatter: Formatter; severity: PackageSeverity }): string {
	const lines = [
		picks.level === defaultLevel ? null : `\t<StanzaLevel>${picks.level}</StanzaLevel>`,
		picks.severity === defaultPackageSeverity || picks.level === 'none' ? null : `\t<StanzaSeverity>${picks.severity}</StanzaSeverity>`,
		picks.formatter === defaultFormatter ? null : `\t<StanzaFormatter>${picks.formatter}</StanzaFormatter>`,
	].filter((line): line is string => line !== null);

	return lines.length === 0 ? '' : ['<PropertyGroup>', ...lines, '</PropertyGroup>'].join('\n');
}
