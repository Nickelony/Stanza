#Requires -Version 5.1
<#
.SYNOPSIS
	Publishes repository documentation to the GitHub wiki.

.DESCRIPTION
	Rule page filenames become wiki paths used by diagnostic help links.
	The wiki repository must exist before this command can run.
	The script publishes src/Nickelony.Stanza.Analyzers/README.md as Home,
	and the headings of its rule tables are the groups of the rule pages in the sidebar.
	It overwrites _Sidebar.md, leaves other pages whose names start with an underscore alone,
	and deletes every remaining .md page that it does not publish.
	A publish runs only from a clean main whose HEAD is the tip of origin/main, because the rule help links open these pages.

.PARAMETER Message
	Commit message for the wiki update; defaults to "Mirror the documentation at <short revision>".
	Either way, the commit names the full revision it mirrors.

.PARAMETER Check
	Checks whether the published wiki matches the repository and fails on drift.

.EXAMPLE
	./wiki.ps1
#>
[CmdletBinding()]
param(
	[string]$Message,
	[switch]$Check
)

$ErrorActionPreference = 'Stop'

Set-StrictMode -Version Latest

$repoRoot = $PSScriptRoot
$readme = Join-Path $repoRoot 'src/Nickelony.Stanza.Analyzers/README.md'
$docsRoot = Join-Path $repoRoot 'docs'

function Get-RepositoryUrl {
	$properties = Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw

	if ($properties -notmatch '<StanzaRepositoryUrl>([^<]+)</StanzaRepositoryUrl>') {
		throw 'Directory.Build.props carries no StanzaRepositoryUrl property.'
	}

	return $Matches[1].Trim()
}

$repositoryUrl = Get-RepositoryUrl
$wikiRemote = "$repositoryUrl.wiki.git"

if (-not (Test-Path -LiteralPath $docsRoot)) {
	throw 'The documentation directory docs/ is missing, so there is nothing to mirror.'
}

# Wiki pages are flat, so every published Markdown page needs a unique name.
$pages = New-Object System.Collections.ArrayList

function Add-Page([string]$group, [string]$name, [string]$text) {
	$first = ($text -split "`n")[0].TrimEnd()

	[void]$pages.Add(
		[pscustomobject]@{
			Group = $group
			Name = $name
			Title = if ($first.StartsWith('# ')) { $first.Substring(2) } else { $name }
			Text = $text.TrimEnd() + "`n"
		}
	)
}

$homePage = [System.IO.File]::ReadAllText($readme).TrimEnd()
Add-Page 'Home' 'Home' "$homePage`n`n---`n`n*Generated from the analyzer readme of [the repository]($repositoryUrl).*"

# Only tracked pages are published: the clean check cannot see a page git ignores.
$trackedPages = @(git -C $repoRoot ls-files -- 'docs/*.md')

if ($LASTEXITCODE -ne 0) {
	throw 'git could not list the tracked pages under docs/.'
}

foreach ($file in $trackedPages | ForEach-Object { Get-Item -LiteralPath (Join-Path $repoRoot $_) } | Sort-Object FullName) {
	# Group each page by its directory name; a directory with no mapping falls under Guides.
	$group = switch ($file.Directory.Name) {
		'rules' { 'Rules' }
		'options' { 'Configuration' }
		'tool' { 'Tool codes' }
		default { 'Guides' }
	}

	Add-Page $group $file.BaseName ([System.IO.File]::ReadAllText($file.FullName))
}

$duplicates = @($pages | Group-Object Name | Where-Object { $_.Count -gt 1 })

if ($duplicates.Count -gt 0) {
	throw "Two pages would share the name '$($duplicates[0].Name)'; a wiki has no directories to tell them apart."
}

# Build the sidebar: the guides, the rule pages by group, the tool codes and the option pages.
$sidebar = New-Object System.Collections.ArrayList
[void]$sidebar.Add('[Home](Home)')
[void]$sidebar.Add('')

# The readme owns the rule groups: each heading of its rule tables names a sidebar section, in the readme's order.
$groupOfRule = @{}
$groupTitles = New-Object System.Collections.ArrayList
$inRules = $false
$current = $null

foreach ($line in $homePage -split '\r?\n') {
	if ($line -match '^## ') {
		$inRules = $line.TrimEnd() -eq '## Rules'
		continue
	}

	if (-not $inRules) {
		continue
	}

	if ($line -match '^### (?<title>.+?)\s*$') {
		$current = $Matches['title']
		[void]$groupTitles.Add($current)
		continue
	}

	if ($line -match '^\| `(?<id>ZA\d{4})` \|') {
		if ($null -eq $current) {
			throw "The readme rule table lists $($Matches['id']) above its first group heading."
		}

		$groupOfRule[$Matches['id']] = $current
	}
}

foreach ($page in @($pages | Where-Object { $_.Group -eq 'Rules' })) {
	if ($page.Name -notmatch '^(?<id>ZA\d{4})' -or -not $groupOfRule.ContainsKey($Matches['id'])) {
		throw "The rule page '$($page.Name)' is in no group of the readme's rule tables."
	}
}

foreach ($group in @('Guides', 'Rules', 'Tool codes', 'Configuration')) {
	[void]$sidebar.Add("**$group**")
	[void]$sidebar.Add('')

	$sections = @(if ($group -eq 'Rules') { $groupTitles } else { '' })

	foreach ($section in $sections) {
		$sectionPages = @(
			$pages |
				Where-Object {
					$_.Group -eq $group -and ($section.Length -eq 0 -or $groupOfRule[$_.Name.Substring(0, 6)] -eq $section)
				} |
				Sort-Object Name
		)

		if ($sectionPages.Count -eq 0) {
			continue
		}

		if ($section.Length -gt 0) {
			[void]$sidebar.Add("*$section*")
		}

		foreach ($page in $sectionPages) {
			[void]$sidebar.Add("- [$($page.Title)]($($page.Name))")
		}

		[void]$sidebar.Add('')
	}
}

$revision = @(git -C $repoRoot rev-parse HEAD)[0]

if ($LASTEXITCODE -ne 0) {
	throw 'HEAD could not be read, so the wiki commit could not name the revision it mirrors.'
}

# The rule help links open these pages, so only a pushed commit of main may reach them.
if (-not $Check) {
	# An untracked file is never published, so it cannot make the wiki commit differ from HEAD.
	$dirty = @(git -C $repoRoot status --porcelain --untracked-files=no)

	if ($LASTEXITCODE -ne 0) {
		throw 'The repository status could not be read, so the wiki cannot tell whether HEAD holds the pages.'
	}

	if ($dirty.Count -gt 0) {
		throw "The working tree has uncommitted changes, so the wiki commit would not match HEAD: $($dirty -join '; ')"
	}

	$branch = @(git -C $repoRoot rev-parse --abbrev-ref HEAD)[0]

	if ($LASTEXITCODE -ne 0) {
		throw 'The current branch could not be read.'
	}

	if ($branch -ne 'main') {
		throw "The wiki is published from main, but HEAD is on '$branch'."
	}

	git -C $repoRoot fetch --quiet origin main

	if ($LASTEXITCODE -ne 0) {
		throw 'origin/main could not be fetched, so the wiki cannot tell whether HEAD has been pushed.'
	}

	# An older HEAD would publish older pages and delete the newer ones, so HEAD must be the tip itself.
	$tip = @(git -C $repoRoot rev-parse origin/main)[0]

	if ($LASTEXITCODE -ne 0) {
		throw 'origin/main could not be read, so the wiki cannot tell whether HEAD is its tip.'
	}

	if ($tip -ne $revision) {
		throw "HEAD is not the tip of origin/main ($tip); pull or push before publishing the wiki."
	}
}

# A clone of its own keeps two runs, or a leftover directory, from mixing their pages.
$work = Join-Path ([System.IO.Path]::GetTempPath()) "stanza-wiki-$([guid]::NewGuid().ToString('N'))"

try {
	Write-Host '==> Cloning the wiki'

	# The pages are written with LF, so a CRLF checkout would show every page as changed.
	git clone --quiet --config core.autocrlf=false $wikiRemote $work

	if ($LASTEXITCODE -ne 0) {
		Write-Host ''
		Write-Host "The wiki repository could not be cloned from $wikiRemote."
		Write-Host 'A wiki exists only once its first page has been created in the browser,'
		Write-Host "at $repositoryUrl/wiki - create one page there by hand, then run this script again."

		exit 1
	}

	Write-Host '==> Writing the pages'

	$removed = @()

	foreach ($file in Get-ChildItem -LiteralPath $work -File -Filter '*.md') {
		# Keep every underscore-prefixed page, such as GitHub's _Sidebar and _Footer, when pruning unowned pages.
		if ($file.BaseName.StartsWith('_')) {
			continue
		}

		if ($pages.Name -notcontains $file.BaseName) {
			$removed += $file.Name
		}

		Remove-Item -LiteralPath $file.FullName
	}

	$encoding = New-Object System.Text.UTF8Encoding($false)

	foreach ($page in $pages) {
		[System.IO.File]::WriteAllText((Join-Path $work "$($page.Name).md"), $page.Text, $encoding)
	}

	[System.IO.File]::WriteAllText((Join-Path $work '_Sidebar.md'), (($sidebar -join "`n").TrimEnd() + "`n"), $encoding)

	$changed = @(git -C $work status --porcelain)

	if ($LASTEXITCODE -ne 0) {
		throw 'The wiki status could not be read.'
	}

	if ($changed.Count -eq 0) {
		Write-Host "==> The wiki already holds this tree ($($pages.Count) pages)."
		exit 0
	}

	Write-Host ''
	Write-Host 'This tree differs from the published wiki:'
	$changed | ForEach-Object { Write-Host "    $_" }

	if ($removed.Count -gt 0) {
		Write-Host "    removed: $($removed -join ', ')"
	}

	if ($Check) {
		Write-Host ''
		Write-Host 'Run ./wiki.ps1 to publish it.'

		exit 1
	}

	Write-Host ''
	Write-Host '==> Committing and pushing'

	if (-not $Message) {
		$Message = "Mirror the documentation at $($revision.Substring(0, 7))"
	}

	git -C $work add --all

	if ($LASTEXITCODE -ne 0) {
		throw 'The wiki pages could not be staged.'
	}

	git -C $work commit --quiet --message $Message --message "Mirrors $revision."

	if ($LASTEXITCODE -ne 0) {
		throw 'The wiki commit failed.'
	}

	git -C $work push --quiet

	if ($LASTEXITCODE -ne 0) {
		throw 'The wiki push failed.'
	}

	Write-Host "==> Published $($pages.Count) pages to $repositoryUrl/wiki"
}
finally {
	if (Test-Path -LiteralPath $work) {
		Remove-Item -LiteralPath $work -Recurse -Force
	}
}
