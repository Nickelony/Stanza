#Requires -Version 5.1
# Dot-sourced by the scripts that read the names of packed files: . (Join-Path $PSScriptRoot 'Get-PackagePattern.ps1')

# Returns the pattern that matches the .nupkg file of a package ID, with the version in the named group `version`.
# The version starts with a digit, so `Nickelony.Stanza.Markup.Core.*` cannot stand in for `Nickelony.Stanza.Markup`.
function Get-PackagePattern([string]$Id) {
	return "^$([regex]::Escape($Id))\.(?<version>\d.*)\.nupkg$"
}
