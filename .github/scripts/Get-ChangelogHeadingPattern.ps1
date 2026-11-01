#Requires -Version 5.1
# Dot-sourced by the scripts that find a version in CHANGELOG.md: . (Join-Path $PSScriptRoot 'Get-ChangelogHeadingPattern.ps1')

# Returns the pattern that matches the changelog heading of a version: `## [<version>]`, with the spaces after the hashes optional.
# With -Dated it matches only the heading of a released version, `## [<version>] - <yyyy-MM-dd>` and nothing after the date.
function Get-ChangelogHeadingPattern([string]$Version, [switch]$Dated) {
	$pattern = "^##\s*\[$([regex]::Escape($Version))\]"

	if ($Dated) {
		$pattern += '\s*-\s*\d{4}-\d{2}-\d{2}\s*$'
	}

	return $pattern
}
