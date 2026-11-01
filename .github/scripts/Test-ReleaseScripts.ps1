#Requires -Version 5.1
<#
.SYNOPSIS
	Checks the changelog steps of the release scripts against the fixtures beside this script.

.DESCRIPTION
	Runs `Add-ChangelogRelease` and the line reading and writing around it, which release.ps1 defines, Assert-ChangelogReleased.ps1 and Get-ChangelogNotes.ps1.
	Each case turns a changelog from fixtures/changelog into the changelog its `.after.txt` file holds, which Assert-ChangelogReleased.ps1 must accept.
	Each released changelog of fixtures/changelog yields the notes its `.expected.txt` file holds, or the failure its case names.
	It also checks the heading patterns that release.ps1, the release workflow and those scripts share.
	It never runs release.ps1 or wiki.ps1, so it changes nothing in the repository.
	Fails with every check that did not hold, and exits non-zero.

.EXAMPLE
	./.github/scripts/Test-ReleaseScripts.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$fixtures = Join-Path $PSScriptRoot 'fixtures/changelog'
$assertScript = Join-Path $PSScriptRoot 'Assert-ChangelogReleased.ps1'
$notesScript = Join-Path $PSScriptRoot 'Get-ChangelogNotes.ps1'

. (Join-Path $PSScriptRoot 'Get-ChangelogHeadingPattern.ps1')
. (Join-Path $PSScriptRoot 'Get-TrackingHeadingPattern.ps1')
$releaseScript = Join-Path $PSScriptRoot '../../release.ps1'
$date = '2030-03-04'

# release.ps1 runs a release when it is invoked, so its helpers are read from the syntax tree and defined here.
$helpers = @('Read-TextLines', 'Write-TextLines', 'Add-ChangelogRelease')
$errors = $null
$tree = [System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path -LiteralPath $releaseScript).Path, [ref] $null, [ref] $errors)

if ($errors.Count -gt 0) {
	throw "release.ps1 does not parse: $($errors[0].Message)"
}

$definitions = @($tree.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -in $helpers }, $true))

if ($definitions.Count -ne $helpers.Count) {
	throw "release.ps1 defines $($definitions.Count) of the functions this test runs ($($helpers -join ', ')), so a rename needs this test to follow."
}

foreach ($definition in $definitions) {
	. ([scriptblock]::Create($definition.Extent.Text))
}

# Each case names the changelog before the release, the version released and the changelog the release writes.
# A prerelease and the stable release after it differ in the link their version gets.
$cases = @(
	@{ Name = 'a first release with the link below the headings'; Before = 'first-release.before.txt'; Version = '1.0.0'; After = 'first-release.after.txt' }
	@{ Name = 'a prerelease'; Before = 'first-release.before.txt'; Version = '1.0.0-rc.1'; After = 'prerelease.after.txt' }
	@{ Name = 'a second release'; Before = 'second-release.before.txt'; Version = '1.1.0'; After = 'second-release.after.txt' }
	@{ Name = 'a release with the link above the headings'; Before = 'link-above.before.txt'; Version = '1.1.0'; After = 'link-above.after.txt' }
	@{ Name = 'the stable release after a prerelease'; Before = 'stable-after-prerelease.before.txt'; Version = '1.0.0'; After = 'stable-after-prerelease.after.txt' }
)

$failures = New-Object System.Collections.Generic.List[string]
$checks = 0

function Assert-Check {
	param(
		[bool] $Condition,

		[string] $Message
	)

	$script:checks++

	if (-not $Condition) {
		$script:failures.Add($Message)
	}
}

# Returns the message the script throws for a changelog, or null when it accepts it.
function Test-Assertion {
	param(
		[string] $Path,

		[string] $Version
	)

	try {
		& $assertScript -Version $Version -Path $Path

		return $null
	}
	catch {
		return $_.Exception.Message
	}
}

# Returns the notes the script reads from a changelog, or the message it throws.
function Get-Notes {
	param(
		[string] $Path,

		[string] $Version
	)

	try {
		return [pscustomobject]@{ Notes = (& $notesScript -Version $Version -Path $Path); Error = $null }
	}
	catch {
		return [pscustomobject]@{ Notes = $null; Error = $_.Exception.Message }
	}
}

# Runs the steps of release.ps1 on a changelog: read, add the release, write, as the script does before it checks the file.
function Invoke-ChangelogRelease {
	param(
		[string] $Path,

		[string] $Version
	)

	$changelog = Read-TextLines -Path $Path

	Add-ChangelogRelease -Lines $changelog.Lines -Version $Version -Date $date
	Write-TextLines -Path $Path -Lines $changelog.Lines -NewLine $changelog.NewLine
}

$work = Join-Path ([System.IO.Path]::GetTempPath()) ("stanza-release-test-" + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $work

try {
	foreach ($case in $cases) {
		$name = $case.Name
		$before = Join-Path $fixtures $case.Before
		$expected = [System.IO.File]::ReadAllText((Join-Path $fixtures $case.After))
		$path = Join-Path $work 'CHANGELOG.md'

		Copy-Item -LiteralPath $before -Destination $path -Force

		# A changelog that is not released yet fails the check, so the check proves nothing unless it fails here.
		Assert-Check ($null -ne (Test-Assertion -Path $path -Version $case.Version)) "$name`: the changelog before the release already passes Assert-ChangelogReleased.ps1."

		try {
			Invoke-ChangelogRelease -Path $path -Version $case.Version
		}
		catch {
			Assert-Check $false "$name`: the release steps failed: $($_.Exception.Message)"

			continue
		}

		$written = [System.IO.File]::ReadAllText($path)

		Assert-Check ($written -ceq $expected) "$name`: the changelog differs from $($case.After).`n--- expected`n$expected`n--- written`n$written"

		$rejection = Test-Assertion -Path $path -Version $case.Version

		Assert-Check ($null -eq $rejection) "$name`: Assert-ChangelogReleased.ps1 rejects the written changelog: $rejection"

		# The version is matched whole, so the changelog of one version is not the release of another.
		$other = if ($case.Version -eq '1.0.0') { '1.0.0-rc.1' } else { '1.0.0' }

		Assert-Check ($null -ne (Test-Assertion -Path $path -Version $other)) "$name`: Assert-ChangelogReleased.ps1 accepts the changelog for $other too."

		# A file with Windows line endings keeps them.
		$windows = Join-Path $work 'CHANGELOG-windows.md'
		$source = [System.IO.File]::ReadAllText($before).Replace("`r`n", "`n").Replace("`n", "`r`n")

		[System.IO.File]::WriteAllText($windows, $source)
		Invoke-ChangelogRelease -Path $windows -Version $case.Version

		$crlf = $expected.Replace("`r`n", "`n").Replace("`n", "`r`n")

		Assert-Check ([System.IO.File]::ReadAllText($windows) -ceq $crlf) "$name`: a changelog with Windows line endings did not keep them."
	}

	# The check refuses a changelog that lacks one of the three things a release needs.
	$released = Join-Path $work 'released.md'
	$text = [System.IO.File]::ReadAllText((Join-Path $fixtures 'second-release.after.txt'))

	$refused = [ordered]@{
		"the '[Unreleased]' link" = $text -replace '(?m)^\[Unreleased\]:.*\r?\n', ''
		"the '[1.1.0]' link" = $text -replace '(?m)^\[1\.1\.0\]:.*\r?\n', ''
		"the '## [Unreleased]' heading" = $text -replace '(?m)^## \[Unreleased\]\r?\n', ''
		'a compare range that names the wrong version' = $text -replace '(?m)^(\[Unreleased\]: .*/compare/v)1\.1\.0', '${1}1.0.0'
		'a heading that is not directly above the release' = $text -replace '(?m)^(## \[1\.1\.0\])', "A line between the headings.`n`n`$1"
	}

	foreach ($name in $refused.Keys) {
		[System.IO.File]::WriteAllText($released, $refused[$name])

		Assert-Check ($null -ne (Test-Assertion -Path $released -Version '1.1.0')) "Assert-ChangelogReleased.ps1 accepts a changelog with $name."
	}

	# The notes come from the heading the check accepts, whatever its spacing, and they end at the next level-two heading.
	$noteCases = @(
		@{ Name = 'a changelog with spaced headings'; Released = 'notes.released.txt'; Expected = 'notes.expected.txt' }
		@{ Name = 'a changelog with headings that have no space after the hashes'; Released = 'notes-compact.released.txt'; Expected = 'notes-compact.expected.txt' }
	)

	foreach ($case in $noteCases) {
		$name = $case.Name
		$path = Join-Path $fixtures $case.Released
		$expected = ([System.IO.File]::ReadAllText((Join-Path $fixtures $case.Expected)) -replace "`r`n", "`n").Trim()
		$rejection = Test-Assertion -Path $path -Version '1.1.0'

		Assert-Check ($null -eq $rejection) "$name`: Assert-ChangelogReleased.ps1 rejects $($case.Released): $rejection"

		$result = Get-Notes -Path $path -Version '1.1.0'

		Assert-Check ($null -eq $result.Error) "$name`: Get-ChangelogNotes.ps1 fails on $($case.Released): $($result.Error)"
		Assert-Check ($result.Notes -ceq $expected) "$name`: the notes differ from $($case.Expected).`n--- expected`n$expected`n--- read`n$($result.Notes)"
	}

	$path = Join-Path $fixtures 'notes.released.txt'

	Assert-Check ($null -ne (Get-Notes -Path $path -Version '1.2.0').Error) 'Get-ChangelogNotes.ps1 reads notes for a version the changelog lacks.'
	Assert-Check ($null -ne (Get-Notes -Path (Join-Path $fixtures 'notes-empty.released.txt') -Version '1.1.0').Error) 'Get-ChangelogNotes.ps1 reads notes from an empty section.'

	# The heading patterns match the version whole.
	$headings = @(
		@{ Pattern = (Get-ChangelogHeadingPattern -Version '1.0.0'); Line = '## [1.0.0] - 2030-01-02'; Matches = $true }
		@{ Pattern = (Get-ChangelogHeadingPattern -Version '1.0.0'); Line = '##[1.0.0]'; Matches = $true }
		@{ Pattern = (Get-ChangelogHeadingPattern -Version '1.0.0'); Line = '## [1.0.0-rc.1] - 2030-01-02'; Matches = $false }
		@{ Pattern = (Get-ChangelogHeadingPattern -Version '1.0.0'); Line = '## [1x0x0] - 2030-01-02'; Matches = $false }
		@{ Pattern = (Get-ChangelogHeadingPattern -Version '1.0.0' -Dated); Line = '##[1.0.0] - 2030-01-02'; Matches = $true }
		@{ Pattern = (Get-ChangelogHeadingPattern -Version '1.0.0' -Dated); Line = '## [1.0.0]'; Matches = $false }
		@{ Pattern = (Get-ChangelogHeadingPattern -Version '1.0.0' -Dated); Line = '## [1.0.0] - 2030-01-02 (yanked)'; Matches = $false }
		@{ Pattern = (Get-TrackingHeadingPattern -Version '1.0.0'); Line = '## Release 1.0.0'; Matches = $true }
		@{ Pattern = (Get-TrackingHeadingPattern -Version '1.0.0'); Line = '## Release 1.0.0-rc.1'; Matches = $false }
		@{ Pattern = (Get-TrackingHeadingPattern -Version '1.0.0'); Line = '## Release 1x0x0'; Matches = $false }
	)

	foreach ($heading in $headings) {
		Assert-Check (($heading.Line -match $heading.Pattern) -eq $heading.Matches) "The pattern $($heading.Pattern) should$(if (-not $heading.Matches) { ' not' }) match '$($heading.Line)'."
	}

	# The steps refuse a changelog they cannot build links from.
	$unfit = [ordered]@{
		"no '## [Unreleased]' heading" = "# Changelog`n`n## [1.0.0] - 2030-01-02`n`n[Unreleased]: https://github.com/example/project/commits/main`n"
		"no '[Unreleased]:' link" = "# Changelog`n`n## [Unreleased]`n`n- A change.`n"
	}

	foreach ($name in $unfit.Keys) {
		$path = Join-Path $work 'unfit.md'

		[System.IO.File]::WriteAllText($path, $unfit[$name])

		$threw = $false

		try {
			Invoke-ChangelogRelease -Path $path -Version '1.0.0'
		}
		catch {
			$threw = $true
		}

		Assert-Check $threw "The release steps accept a changelog with $name."
	}
}
finally {
	Remove-Item -LiteralPath $work -Recurse -Force
}

if ($failures.Count -gt 0) {
	throw "$($failures.Count) of $checks release script checks failed:`n$($failures -join "`n")"
}

Write-Host "Release script checks passed: $checks."
