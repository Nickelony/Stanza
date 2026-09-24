#Requires -Version 5.1
<#
.SYNOPSIS
	Finds a packed .nupkg by package ID and returns it with its version.

.DESCRIPTION
	Returns the file with a Version note property, read from the file name after the id.
	Fails when the directory holds no package for the id.

.PARAMETER Id
	The package ID, such as Nickelony.Stanza.Markup.

.PARAMETER Directory
	The directory the pack wrote to; defaults to artifacts.

.EXAMPLE
	(./.github/scripts/Find-Package.ps1 -Id Nickelony.Stanza.Markup).Version
#>
[CmdletBinding()]
param(
	[Parameter(Mandatory = $true)]
	[string] $Id,

	[string] $Directory = 'artifacts'
)

$ErrorActionPreference = 'Stop'

Set-StrictMode -Version Latest

# The version starts with a digit, so `Nickelony.Stanza.Markup.Core.*` cannot stand in for `Nickelony.Stanza.Markup`.
$pattern = "^$([regex]::Escape($Id))\.(?<version>\d.*)\.nupkg$"

foreach ($file in Get-ChildItem -Path $Directory -File) {
	$match = [regex]::Match($file.Name, $pattern)

	if ($match.Success) {
		return $file | Add-Member -NotePropertyName Version -NotePropertyValue $match.Groups['version'].Value -PassThru
	}
}

throw "No $Id package is in $Directory."
