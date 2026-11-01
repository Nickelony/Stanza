#Requires -Version 5.1
<#
.SYNOPSIS
	Checks that the changelog is ready for the release of a version.

.DESCRIPTION
	Fails unless CHANGELOG.md holds an empty `## [Unreleased]` heading directly above the `## [<version>] - <date>` heading,
	and the two link references a release needs:
	`[<version>]` links its compare range, or its release tag for the first release, and `[Unreleased]` links `compare/v<version>...HEAD`.
	release.ps1 writes them, and the release workflow checks them again once the tag is pushed.

.PARAMETER Version
	The released version, in the form x.y.z with an optional `-` suffix and no leading `v`.

.PARAMETER Path
	The changelog to check; defaults to CHANGELOG.md at the root of the repository.

.EXAMPLE
	./.github/scripts/Assert-ChangelogReleased.ps1 -Version 0.9.0
#>
[CmdletBinding()]
param(
	[Parameter(Mandatory = $true)]
	[string] $Version,

	[string] $Path = (Join-Path $PSScriptRoot '../../CHANGELOG.md')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot 'Get-ChangelogHeadingPattern.ps1')

$escaped = [regex]::Escape($Version)
$lines = @(Get-Content -LiteralPath $Path)

# A version links a compare range, or the release tag for the first release.
$expected = [ordered]@{
	"the '## [Unreleased]' heading" = '^##\s*\[Unreleased\]\s*$'
	"the '[$Version]' link" = "^\[$escaped\]: https://github\.com/[^/]+/[^/]+/(compare/v\S+\.\.\.v$escaped|releases/tag/v$escaped)\s*$"
	"the '[Unreleased]' link" = "^\[Unreleased\]: https://github\.com/[^/]+/[^/]+/compare/v$escaped\.\.\.HEAD\s*$"
}

$missing = @()

foreach ($name in $expected.Keys) {
	if (-not ($lines | Where-Object { $_ -match $expected[$name] })) {
		$missing += $name
	}
}

if ($missing.Count -gt 0) {
	throw "$Path is not ready for the release of ${Version}; it lacks $($missing -join ', ')."
}

# The empty Unreleased section sits directly above the section of the version, with nothing between their headings.
$headingIndex = 0

while ($lines[$headingIndex] -notmatch $expected["the '## [Unreleased]' heading"]) {
	$headingIndex++
}

$nextIndex = $headingIndex + 1

while ($nextIndex -lt $lines.Count -and [string]::IsNullOrWhiteSpace($lines[$nextIndex])) {
	$nextIndex++
}

if ($nextIndex -ge $lines.Count -or $lines[$nextIndex] -notmatch (Get-ChangelogHeadingPattern -Version $Version -Dated)) {
	throw "$Path is not ready for the release of ${Version}; the '## [Unreleased]' heading is not directly followed by the '## [$Version] - <date>' heading."
}
