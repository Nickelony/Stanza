#Requires -Version 5.1
<#
.SYNOPSIS
	Reads the version a release tag names.

.DESCRIPTION
	Returns the version of a tag in the form v<major>.<minor>.<patch> with an optional `-` suffix, without the `v`.
	Fails when the tag has any other form.

.PARAMETER Tag
	The tag name, such as v1.0.0 or v1.0.0-rc.1.

.EXAMPLE
	./.github/scripts/Get-ReleaseVersion.ps1 -Tag v1.0.0
#>
[CmdletBinding()]
param(
	[Parameter(Mandatory = $true)]
	[string] $Tag
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# One leading `v` is dropped; trimming repeated prefixes could accept a malformed tag.
$match = [regex]::Match($Tag, '^v(?<version>\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?)\z')

if (-not $match.Success) {
	throw "The tag '$Tag' is not a v-prefixed version, so it cannot be released."
}

return $match.Groups['version'].Value
