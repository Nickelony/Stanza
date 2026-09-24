#Requires -Version 5.1
<#
.SYNOPSIS
	Fails when a test run skipped a test that does not state its skip as a design.

.DESCRIPTION
	Reads the .trx files of a `dotnet test --logger trx` run.
	A test skips by design when it raises ExpectedSkip.Skip, as the soak and C# 14 tests do,
	or when its own [OSCondition] leaves it out on this system.
	Any other skip, such as a test that cannot make a link or deny a directory here, fails the run,
	because that test would otherwise prove nothing and say so nowhere.

.PARAMETER Results
	The directory the run wrote its .trx files to.

.PARAMETER Soak
	Set for a soak run, where the tests of the Soak category must have run and not skipped.

.EXAMPLE
	./.github/scripts/Assert-ExpectedSkips.ps1 -Results $env:RUNNER_TEMP/test-results
#>
[CmdletBinding()]
param(
	[Parameter(Mandatory = $true)]
	[string] $Results,

	[switch] $Soak
)

$ErrorActionPreference = 'Stop'

Set-StrictMode -Version Latest

# The text ExpectedSkip.Skip writes in tests/Shared; a drift makes every soak and C# 14 skip fail here, so it cannot go unseen.
$marker = 'Expected skip:'

# What MSTest writes for a test its [OSCondition] leaves out.
$operatingSystem = '^Test is (only |not )?supported on '

$files = @(Get-ChildItem -LiteralPath $Results -Filter *.trx -File -Recurse)

if ($files.Count -eq 0) {
	throw "$Results holds no .trx file, so the skips of the run cannot be read."
}

$skipped = 0
$soakRan = 0
$unexpected = @()

foreach ($file in $files) {
	$document = New-Object System.Xml.XmlDocument
	$document.Load($file.FullName)

	$tests = @{}

	foreach ($test in $document.GetElementsByTagName('UnitTest')) {
		$method = $test.GetElementsByTagName('TestMethod')[0]

		$tests[$test.GetAttribute('id')] = [pscustomobject]@{
			Name = "$($method.GetAttribute('className')).$($test.GetAttribute('name'))"
			Categories = @($test.GetElementsByTagName('TestCategoryItem') | ForEach-Object { $_.GetAttribute('TestCategory') })
		}
	}

	foreach ($result in $document.GetElementsByTagName('UnitTestResult')) {
		$test = $tests[$result.GetAttribute('testId')]

		if (-not $test) {
			$test = [pscustomobject]@{ Name = $result.GetAttribute('testName'); Categories = @() }
		}

		$outcome = $result.GetAttribute('outcome')

		if ($outcome -eq 'Passed') {
			if ($test.Categories -contains 'Soak') {
				$soakRan++
			}

			continue
		}

		if ($outcome -notin 'NotExecuted', 'Inconclusive') {
			continue
		}

		$skipped++

		$node = $result.GetElementsByTagName('Message') | Select-Object -First 1
		$message = if ($node) { $node.InnerText } else { '' }
		$byDesign = $message.Contains($marker) -or $message -match $operatingSystem

		# A soak run exists to run these tests, so a skip of one defeats it.
		if ($Soak -and $test.Categories -contains 'Soak') {
			$byDesign = $false
		}

		if (-not $byDesign) {
			$unexpected += "$($test.Name): $(($message -split "`r?`n")[0])"
		}
	}
}

Write-Host "Read $($files.Count) result file(s): $skipped test(s) skipped, $($unexpected.Count) not by design."

if ($unexpected.Count -gt 0) {
	throw "These tests skipped without stating a design, so this run did not exercise them:`n  $($unexpected -join "`n  ")"
}

if ($Soak -and $soakRan -eq 0) {
	throw 'No test of the Soak category ran, so the soak exercised nothing.'
}
