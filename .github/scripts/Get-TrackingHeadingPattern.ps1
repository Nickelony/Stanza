#Requires -Version 5.1
# Dot-sourced by the scripts that find a version in AnalyzerReleases.Shipped.md: . (Join-Path $PSScriptRoot 'Get-TrackingHeadingPattern.ps1')

# Returns the pattern that matches the `## Release <version>` heading of the analyzer release tracking file, and nothing after the version.
function Get-TrackingHeadingPattern([string]$Version) {
	return "^## Release $([regex]::Escape($Version))\s*$"
}
