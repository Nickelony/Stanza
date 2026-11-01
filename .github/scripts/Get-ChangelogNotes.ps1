#Requires -Version 5.1
<#
.SYNOPSIS
	Reads the changelog notes of a released version.

.DESCRIPTION
	Returns the body of the `## [<version>] - <date>` section of CHANGELOG.md, which becomes the GitHub release body.
	The section ends at the next level-two heading, and the link reference definitions are dropped.
	Finds the heading with the pattern Assert-ChangelogReleased.ps1 checks, so a changelog that passes that check has notes here.
	Fails when the changelog has no section for the version or the section has no notes.

.PARAMETER Version
	The released version, in the form x.y.z with an optional `-` suffix and no leading `v`.

.PARAMETER Path
	The changelog to read; defaults to CHANGELOG.md at the root of the repository.

.EXAMPLE
	./.github/scripts/Get-ChangelogNotes.ps1 -Version 0.9.0
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

$heading = "## [$Version] - <date>"
$pattern = Get-ChangelogHeadingPattern -Version $Version -Dated
$lines = @(Get-Content -LiteralPath $Path)
$start = -1

for ($index = 0; $index -lt $lines.Count; $index++) {
	if ($lines[$index] -match $pattern) {
		$start = $index
		break
	}
}

if ($start -lt 0) {
	throw "$Path carries no '$heading' section, so the release would be published with no notes."
}

$end = $lines.Count

for ($index = $start + 1; $index -lt $lines.Count; $index++) {
	if ($lines[$index] -match '^##(?!#)') {
		$end = $index
		break
	}
}

# Avoid a descending index range when the section has no body.
$body = if ($end -gt $start + 1) { @($lines[($start + 1)..($end - 1)]) } else { @() }

# Drop the reference definitions only; the blank lines stay, so a paragraph after a list keeps its break.
$notes = (@($body | Where-Object { $_ -notmatch '^\[[^\]]+\]:' }) -join "`n").Trim()

if ($notes.Length -eq 0) {
	throw "$Path's '$heading' section is empty, so the release would be published with no notes."
}

return $notes
