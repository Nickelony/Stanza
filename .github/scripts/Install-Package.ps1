#Requires -Version 5.1
<#
.SYNOPSIS
	Installs a package this run packed, so no other source can stand in for it.

.DESCRIPTION
	The analyzer package is added to the project in the current directory.
	A tool goes to -ToolPath, so the repository's tool manifest stays as committed.

.PARAMETER Id
	The package id.

.PARAMETER ToolPath
	The directory to install a tool into; a tool needs one.

.PARAMETER Directory
	The directory the pack wrote to; defaults to artifacts.

.EXAMPLE
	./.github/scripts/Install-Package.ps1 -Id Nickelony.Stanza.Markup -ToolPath $tools
#>
[CmdletBinding()]
param(
	[Parameter(Mandatory = $true)]
	[string] $Id,

	[string] $ToolPath,

	[string] $Directory = 'artifacts'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# A relative directory would resolve against the project the analyzer is added to.
$source = (Resolve-Path -LiteralPath $Directory).Path
$version = (& (Join-Path $PSScriptRoot 'Find-Package.ps1') -Id $Id -Directory $source).Version

# NuGet reuses a cached package of the same ID and version, and an earlier build may have packed this version.
$locals = @(dotnet nuget locals global-packages --list)

if ($LASTEXITCODE -ne 0) {
	throw "Listing the NuGet package cache failed (exit code $LASTEXITCODE)."
}

$cache = ($locals[-1] -replace '^global-packages:\s*', '').Trim()
$cached = Join-Path (Join-Path $cache $Id.ToLowerInvariant()) $version.ToLowerInvariant()

if (Test-Path -LiteralPath $cached) {
	Remove-Item -LiteralPath $cached -Recurse -Force
}

# `--source` replaces the configured sources, so only the package this run packed can resolve.
if ($Id -eq 'Nickelony.Stanza.Analyzers') {
	dotnet add package $Id --version $version --source $source | Out-Host
}
elseif (-not $ToolPath) {
	throw "Installing $Id needs -ToolPath, so the repository's tool manifest stays as committed."
}
else {
	dotnet tool install $Id --tool-path $ToolPath --source $source --version $version | Out-Host
}

if ($LASTEXITCODE -ne 0) {
	throw "Installing $Id $version from $source failed (exit code $LASTEXITCODE)."
}
