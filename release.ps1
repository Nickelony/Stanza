#Requires -Version 5.1
<#
.SYNOPSIS
	Prepares the analyzer release tracking files and the changelog.

.DESCRIPTION
	Preserves each file's line endings. A changelog section that already exists is reported and left unchanged.
	The changelog gets the new section under an empty `## [Unreleased]` heading, and the link references of both versions.
	A prerelease leaves the release tracking files alone, because the release tracking analyzer refuses a suffix in a release heading.
	Fails when a file is missing, -Version or -Date is invalid, a write fails, or any of these holds:
	- AnalyzerReleases.Shipped.md already has a section for the version.
	- AnalyzerReleases.Unshipped.md has no `###` section.
	- AnalyzerReleases.Unshipped.md has no rule rows, unless `-AllowNoRules` is set.
	- CHANGELOG.md has neither a section for the version nor a `## [Unreleased]` heading.
	- CHANGELOG.md has no `[Unreleased]:` link reference to build the version links from.
	- The working tree has uncommitted changes.

.PARAMETER Version
	Release version used in the output headings, in the form x.y.z with an optional `-` suffix and no leading `v`.

.PARAMETER Date
	Changelog date in yyyy-MM-dd form; defaults to today.

.PARAMETER AllowNoRules
	Allows an empty analyzer release section.

.EXAMPLE
	./release.ps1 -Version 0.9.0
#>
[CmdletBinding()]
param(
	[Parameter(Mandatory = $true)]
	[string] $Version,

	[string] $Date = (Get-Date -Format 'yyyy-MM-dd'),

	[switch] $AllowNoRules
)

$ErrorActionPreference = 'Stop'

Set-StrictMode -Version Latest

$repoRoot = $PSScriptRoot
$analyzerDirectory = Join-Path $repoRoot 'src/Nickelony.Stanza.Analyzers'
$shippedPath = Join-Path $analyzerDirectory 'AnalyzerReleases.Shipped.md'
$unshippedPath = Join-Path $analyzerDirectory 'AnalyzerReleases.Unshipped.md'
$changelogPath = Join-Path $repoRoot 'CHANGELOG.md'

$ruleRow = '^\s*ZA\d{4}\s*\|'

# The workflow finds the same headings with the same patterns, so a heading this script accepts is one the workflow accepts.
. (Join-Path $repoRoot '.github/scripts/Get-ChangelogHeadingPattern.ps1')
. (Join-Path $repoRoot '.github/scripts/Get-TrackingHeadingPattern.ps1')

# Returns the line ending the file uses beside its lines, so a write can keep it.
function Read-TextLines {
	param(
		[Parameter(Mandatory = $true)]
		[string] $Path
	)

	$text = [System.IO.File]::ReadAllText($Path)
	$newLine = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
	$lines = [System.Collections.Generic.List[string]]::new()

	foreach ($line in $text.TrimEnd("`r", "`n") -split "`r?`n") {
		$lines.Add($line)
	}

	return [pscustomobject]@{ Lines = $lines; NewLine = $newLine }
}

function Write-TextLines {
	param(
		[Parameter(Mandatory = $true)]
		[string] $Path,

		# Not mandatory, because a mandatory parameter refuses an empty collection.
		[System.Collections.Generic.List[string]] $Lines,

		[Parameter(Mandatory = $true)]
		[string] $NewLine
	)

	# Write UTF-8 without a BOM, as the tracking files and the changelog are kept.
	$encoding = New-Object System.Text.UTF8Encoding($false)

	[System.IO.File]::WriteAllText($Path, (($Lines -join $NewLine) + $NewLine), $encoding)
}

function Remove-TrailingBlankLines {
	param(
		# Not mandatory, because a mandatory parameter refuses an empty array.
		[string[]] $Lines
	)

	$end = $Lines.Count

	while ($end -gt 0 -and $Lines[$end - 1].Trim().Length -eq 0) {
		$end -= 1
	}

	# Avoid a descending index range, which PowerShell enumerates backwards.
	return $(if ($end -eq 0) { @() } else { @($Lines[0..($end - 1)]) })
}

# Retitles the Unreleased section for the version, opens an empty Unreleased section above it and writes the links of both versions.
# The links are the ones the release workflow checks: the version links its compare range from the last release, or its tag for the first.
function Add-ChangelogRelease {
	param(
		# Not mandatory, because a mandatory parameter refuses a collection that holds an empty line.
		[System.Collections.Generic.List[string]] $Lines,

		[Parameter(Mandatory = $true)]
		[string] $Version,

		[Parameter(Mandatory = $true)]
		[string] $Date
	)

	$headingIndex = -1
	$linkIndex = -1

	for ($index = 0; $index -lt $Lines.Count; $index++) {
		if ($headingIndex -lt 0 -and $Lines[$index] -match '^##\s*\[Unreleased\]\s*$') {
			$headingIndex = $index
		}
		elseif ($linkIndex -lt 0 -and $Lines[$index] -match '^\[Unreleased\]:\s*(?<base>https://github\.com/[^/\s]+/[^/\s]+)/(?<target>\S+)\s*$') {
			$linkIndex = $index
			$base = $Matches['base']
			$target = $Matches['target']
		}
	}

	if ($headingIndex -lt 0) {
		throw "CHANGELOG.md carries neither a section for $Version nor a '## [Unreleased]' heading, so there is nothing to retitle."
	}

	if ($linkIndex -lt 0) {
		throw "CHANGELOG.md carries no '[Unreleased]:' link reference, so the links of $Version cannot be written."
	}

	# The Unreleased link of a changelog that has released before names the last release; before the first release it names no tag.
	$previous = if ($target -match '^compare/v(?<previous>\S+)\.\.\.HEAD$') { $Matches['previous'] } else { $null }

	$versionLink = if ($null -eq $previous) { "$base/releases/tag/v${Version}" } else { "$base/compare/v${previous}...v${Version}" }

	$Lines[$linkIndex] = "[Unreleased]: $base/compare/v${Version}...HEAD"
	$Lines.Insert($linkIndex + 1, "[$Version]: $versionLink")

	# A link above the heading pushed the heading down by the line it gained.
	if ($linkIndex -lt $headingIndex) {
		$headingIndex++
	}

	$Lines.Insert($headingIndex + 1, '')
	$Lines.Insert($headingIndex + 2, "## [$Version] - $Date")
}

# The workflow reads the pushed tag with the same script, so a version this script accepts is one the workflow accepts.
try {
	$null = & (Join-Path $repoRoot '.github/scripts/Get-ReleaseVersion.ps1') -Tag "v$Version"
}
catch {
	throw "'$Version' is not a version. Pass the version of the tag that will be pushed, for example 0.9.0."
}

if ($Date -notmatch '^\d{4}-\d{2}-\d{2}$') {
	throw "'$Date' is not a date in yyyy-MM-dd form."
}

foreach ($path in @($shippedPath, $unshippedPath, $changelogPath)) {
	if (-not (Test-Path -LiteralPath $path)) {
		throw "'$path' does not exist. Keep this script inside the repository it releases."
	}
}

# Unrelated changes would muddy the diff that the release asks the reader to review; an untracked file stays out of that diff.
$dirty = @(git -C $repoRoot status --porcelain --untracked-files=no)

if ($LASTEXITCODE -ne 0) {
	throw 'The repository status could not be read, so the working tree cannot be checked.'
}

if ($dirty.Count -gt 0) {
	throw "The working tree has uncommitted changes, so the release diff would not hold only the release: $($dirty -join '; ')"
}

$shipped = Read-TextLines -Path $shippedPath
$unshipped = Read-TextLines -Path $unshippedPath
$changelog = Read-TextLines -Path $changelogPath
$prerelease = $Version.Contains('-')

# --- the release tracking files --------------------------------------------------------------------

if (-not $prerelease) {
	if (@($shipped.Lines | Where-Object { $_ -match (Get-TrackingHeadingPattern -Version $Version) }).Count -gt 0) {
		throw "AnalyzerReleases.Shipped.md already carries a '## Release $Version' section, so its rule rows have been moved already."
	}

	$sectionStart = -1

	for ($index = 0; $index -lt $unshipped.Lines.Count; $index++) {
		if ($unshipped.Lines[$index] -match '^###\s') {
			$sectionStart = $index
			break
		}
	}

	if ($sectionStart -lt 0) {
		throw "AnalyzerReleases.Unshipped.md carries no '### ' section, so there is nothing to move and its shape is not the one this script understands."
	}

	$body = @($unshipped.Lines[$sectionStart..($unshipped.Lines.Count - 1)])
	$rows = @($body | Where-Object { $_ -match $ruleRow })
	$section = @(Remove-TrailingBlankLines -Lines $body)
	$remaining = @(Remove-TrailingBlankLines -Lines @($body | Where-Object { $_ -notmatch $ruleRow }))

	$head = @()
	$bodyStart = 0

	while (
		$bodyStart -lt $shipped.Lines.Count -and
		($shipped.Lines[$bodyStart].StartsWith(';') -or $shipped.Lines[$bodyStart].Trim().Length -eq 0)
	) {
		if ($shipped.Lines[$bodyStart].StartsWith(';')) {
			$head += $shipped.Lines[$bodyStart]
		}

		$bodyStart += 1
	}

	$rest = @()

	if ($bodyStart -lt $shipped.Lines.Count) {
		$rest = @(Remove-TrailingBlankLines -Lines @($shipped.Lines[$bodyStart..($shipped.Lines.Count - 1)]))
	}

	$newShipped = [System.Collections.Generic.List[string]]::new()

	foreach ($line in $head) {
		$newShipped.Add($line)
	}

	$newShipped.Add('')
	$newShipped.Add("## Release $Version")
	$newShipped.Add('')

	foreach ($line in $section) {
		$newShipped.Add($line)
	}

	if ($rest.Count -gt 0) {
		$newShipped.Add('')

		foreach ($line in $rest) {
			$newShipped.Add($line)
		}
	}

	$newUnshipped = [System.Collections.Generic.List[string]]::new()

	for ($index = 0; $index -lt $sectionStart; $index++) {
		$newUnshipped.Add($unshipped.Lines[$index])
	}

	foreach ($line in $remaining) {
		$newUnshipped.Add($line)
	}

	if ($rows.Count -eq 0 -and -not $AllowNoRules) {
		throw "AnalyzerReleases.Unshipped.md carries no rule row, so the section this release would add names no rule. Pass -AllowNoRules when the release really has no analyzer change."
	}
}

# --- the changelog heading -------------------------------------------------------------------------

$writeChangelog = $false

if (@($changelog.Lines | Where-Object { $_ -match (Get-ChangelogHeadingPattern -Version $Version) }).Count -gt 0) {
	$changelogNote = "CHANGELOG.md already carries a section for $Version, so it was left untouched"
}
else {
	Add-ChangelogRelease -Lines $changelog.Lines -Version $Version -Date $Date

	$changelogNote = "'## [Unreleased]' is now '## [$Version] - $Date' under a new empty '## [Unreleased]', and the links of both versions are written"
	$writeChangelog = $true
}

# --- what is written -------------------------------------------------------------------------------

# Prepare all contents before writing, then restore each file's original bytes if a write fails.
# The shipped section blocks reruns, so rollback prevents a failed release from becoming partial.
$shippedBefore = [System.IO.File]::ReadAllBytes($shippedPath)
$unshippedBefore = [System.IO.File]::ReadAllBytes($unshippedPath)
$changelogBefore = [System.IO.File]::ReadAllBytes($changelogPath)

try {
	if (-not $prerelease) {
		Write-TextLines -Path $shippedPath -Lines $newShipped -NewLine $shipped.NewLine
		Write-TextLines -Path $unshippedPath -Lines $newUnshipped -NewLine $unshipped.NewLine
	}

	if ($writeChangelog) {
		Write-TextLines -Path $changelogPath -Lines $changelog.Lines -NewLine $changelog.NewLine

		# The workflow runs the same script once the tag is pushed, so a file that passes here passes there.
		& (Join-Path $repoRoot '.github/scripts/Assert-ChangelogReleased.ps1') -Version $Version -Path $changelogPath
	}
}
catch {
	[System.IO.File]::WriteAllBytes($shippedPath, $shippedBefore)
	[System.IO.File]::WriteAllBytes($unshippedPath, $unshippedBefore)
	[System.IO.File]::WriteAllBytes($changelogPath, $changelogBefore)

	throw "The release could not be written, so every file was put back as it was: $($_.Exception.Message)"
}

# --- what happened ---------------------------------------------------------------------------------

if ($prerelease) {
	Write-Host ''
	Write-Host '==> AnalyzerReleases.Shipped.md and AnalyzerReleases.Unshipped.md' -ForegroundColor Cyan
	Write-Host "    left alone: $Version is a prerelease, so its rules stay unshipped until the stable release"
}
else {
	Write-Host ''
	Write-Host "==> AnalyzerReleases.Shipped.md" -ForegroundColor Cyan
	Write-Host "    added a '## Release $Version' section before the releases already there"

	Write-Host ''
	Write-Host "==> AnalyzerReleases.Unshipped.md" -ForegroundColor Cyan

	if ($rows.Count -eq 0) {
		Write-Host '    carried no rule row (-AllowNoRules), so nothing was moved out of it'
	}
	else {
		Write-Host "    moved $($rows.Count) rule row(s) out, leaving the table header behind"
	}
}

Write-Host ''
Write-Host "==> CHANGELOG.md" -ForegroundColor Cyan
Write-Host "    $changelogNote"

if ($writeChangelog -eq $false) {
	# A section left as written may lack the heading or the links, which the workflow refuses after the tag is pushed.
	try {
		& (Join-Path $repoRoot '.github/scripts/Assert-ChangelogReleased.ps1') -Version $Version -Path $changelogPath
	}
	catch {
		Write-Host "    $($_.Exception.Message)" -ForegroundColor Yellow
	}
}

Write-Host ''
Write-Host 'Nothing was committed and nothing was tagged: review the diff with `git diff`, commit it, publish the wiki and push the tag.' -ForegroundColor Yellow
