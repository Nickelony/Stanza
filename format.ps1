#Requires -Version 5.1
<#
.SYNOPSIS
	Formats repository files with stanza-format and stanza-markup, built from this repository.

.DESCRIPTION
	Without a switch, formats the files git reports as changed, as docs/Format-command-reference.md says,
	then every markup file of the repository.
	Both tools are built from their projects first, and that build builds the Stanza analyzers in Debug,
	the output `dotnet format` loads, so a run never applies a stale rule.
	docs/Format-command-reference.md lists the steps and the checks.

.PARAMETER All
	Formats the whole solution.

.PARAMETER Check
	Checks the whole solution and the markup files without writing, and fails when a step would change a file.
	Both checks run whatever the first reports, and the exit code is the worse of the two.

.EXAMPLE
	./format.ps1
#>
[CmdletBinding()]
param(
	[switch]$All,
	[switch]$Check
)

# The error preference stays Continue: each exit code is checked below,
# and under Stop, Windows PowerShell would end the run at a line the tool writes to a redirected error stream.
Set-StrictMode -Version Latest

$format = Join-Path $PSScriptRoot 'src/Nickelony.Stanza.Format/Nickelony.Stanza.Format.csproj'
$markup = Join-Path $PSScriptRoot 'src/Nickelony.Stanza.Markup/Nickelony.Stanza.Markup.csproj'

# The build runs no analyzer, so a layout the run is about to fix in the tools' own sources cannot stop them from building.
foreach ($project in @($format, $markup)) {
	& dotnet build $project -c Debug --nologo --verbosity quiet -p:RunAnalyzers=false -p:TreatWarningsAsErrors=false

	if ($LASTEXITCODE -ne 0) {
		Write-Host "Failed to build $(Split-Path $project -Leaf) and the Stanza analyzers." -ForegroundColor Red
		exit $LASTEXITCODE
	}
}

$arguments = @()

if ($All) {
	$arguments += '--all'
}

if ($Check) {
	$arguments += '--check'
}

& dotnet run --project $format -c Debug --no-build -- @arguments $PSScriptRoot
$formatExitCode = $LASTEXITCODE

# A write run that failed leaves the markup files for the next run, but a check reports what each tool finds.
if ($formatExitCode -ne 0 -and -not $Check) {
	exit $formatExitCode
}

# The tool manifest pins released tools only, so stanza-format finds no stanza-markup there, and the one built here runs instead.
$command = if ($Check) { @('check', '.', '--strict') } else { @('format', '.') }

Push-Location $PSScriptRoot

try {
	& dotnet run --project $markup -c Debug --no-build -- @command
	$markupExitCode = $LASTEXITCODE
}
finally {
	Pop-Location
}

exit [Math]::Max($formatExitCode, $markupExitCode)
