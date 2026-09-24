#Requires -Version 5.1
<#
.SYNOPSIS
	Fails unless a directory holds exactly one .nupkg for each package ID that ships, and no other .nupkg.

.DESCRIPTION
	package-ids.txt beside this script lists the IDs, and PackageLayoutTests reads the same file,
	so the packages a publish sends are the ones the tests pack.

.PARAMETER Directory
	The directory the pack wrote to; defaults to artifacts.

.EXAMPLE
	./.github/scripts/Assert-PackageSet.ps1
#>
[CmdletBinding()]
param(
	[string] $Directory = 'artifacts'
)

$ErrorActionPreference = 'Stop'

Set-StrictMode -Version Latest

$ids = @(
	Get-Content -LiteralPath (Join-Path $PSScriptRoot 'package-ids.txt') |
		ForEach-Object { $_.Trim() } |
		Where-Object { $_.Length -gt 0 }
)

if ($ids.Count -eq 0) {
	throw 'package-ids.txt names no package.'
}

$files = @(Get-ChildItem -Path $Directory -Filter *.nupkg -File)
$claimed = @()

foreach ($id in $ids) {
	# The version starts with a digit, so `Nickelony.Stanza.Markup.Core.*` cannot stand in for `Nickelony.Stanza.Markup`.
	$pattern = "^$([regex]::Escape($id))\.\d.*\.nupkg$"
	$found = @($files | Where-Object { $_.Name -match $pattern })

	if ($found.Count -ne 1) {
		throw "$Directory holds $($found.Count) package(s) of $id, and a publish needs exactly one."
	}

	$claimed += $found[0].Name
}

$extra = @($files | Where-Object { $claimed -notcontains $_.Name })

if ($extra.Count -gt 0) {
	throw "$Directory holds packages that package-ids.txt does not name, so a publish would send them: $($extra.Name -join ', ')."
}

Write-Host "$Directory holds exactly the packages of package-ids.txt: $($claimed -join ', ')."
