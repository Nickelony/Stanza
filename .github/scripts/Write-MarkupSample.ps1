#Requires -Version 5.1
<#
.SYNOPSIS
	Writes a markup file that lacks one blank line, which stanza-markup reports and then adds.

.PARAMETER Path
	The file to write.

.EXAMPLE
	./.github/scripts/Write-MarkupSample.ps1 -Path sample.xml
#>
[CmdletBinding()]
param(
	[Parameter(Mandatory = $true)]
	[string] $Path
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# The multi-line <a> and the <c> after it share no blank line, which ZA0101 asks for.
Set-Content -Path $Path -Value @(
	'<root>'
	'  <a>'
	'    <b/>'
	'  </a>'
	'  <c/>'
	'</root>'
)
