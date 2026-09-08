#Requires -Version 5.1
<#
.SYNOPSIS
Checks local public-documentation links, heading anchors, and language pairs without network access.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$documentRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$files = @('README.md', 'README.en.md', 'PANDUAN.md', 'THIRD_PARTY_NOTICES.md', 'licenses/README.md') |
    ForEach-Object { Get-Item -LiteralPath (Join-Path $documentRoot $_) }
$files += @(Get-ChildItem -LiteralPath (Join-Path $documentRoot 'docs') -Filter '*.md' -File -Recurse)
$anchorCache = @{}
$linkCount = 0
$failures = [Collections.Generic.List[string]]::new()

function Get-MarkdownAnchors([string]$Path) {
    if ($anchorCache.ContainsKey($Path)) { return ,$anchorCache[$Path] }
    $anchors = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $counts = @{}
    $insideFence = $false
    foreach ($line in Get-Content -LiteralPath $Path -Encoding UTF8) {
        if ($line -match '^\s*(```|~~~)') { $insideFence = -not $insideFence; continue }
        if ($insideFence -or $line -notmatch '^#{1,6}\s+(.+?)\s*#*\s*$') { continue }
        $heading = $Matches[1] -replace '\[([^\]]+)\]\([^)]+\)', '$1' -replace '<[^>]+>', ''
        $slug = ($heading.ToLowerInvariant() -replace '[^\p{L}\p{N}_\-\s]', '') -replace '\s', '-'
        if ($counts.ContainsKey($slug)) { $counts[$slug]++; $slug += '-' + $counts[$slug] }
        else { $counts[$slug] = 0 }
        $null = $anchors.Add($slug)
    }
    $anchorCache[$Path] = $anchors
    return ,$anchors
}

foreach ($file in $files) {
    $body = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    foreach ($match in [regex]::Matches($body, '\[[^\]\r\n]*\]\(([^)\r\n]+)\)')) {
        $target = $match.Groups[1].Value.Trim()
        if ($target -match '^(https?://|mailto:)') { continue }
        $parts = $target -split '#', 2
        $relative = [Uri]::UnescapeDataString($parts[0].Trim('<', '>'))
        $path = if ($relative) { [IO.Path]::GetFullPath((Join-Path $file.DirectoryName $relative)) } else { $file.FullName }
        $linkCount++
        if (-not $path.StartsWith($documentRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
            $failures.Add("Link leaves the repository: $($file.Name) -> $target"); continue
        }
        if (-not (Test-Path -LiteralPath $path)) { $failures.Add("Missing target: $($file.Name) -> $target"); continue }
        if ($parts.Count -gt 1 -and $parts[1] -and [IO.Path]::GetExtension($path) -eq '.md') {
            $anchor = [Uri]::UnescapeDataString($parts[1])
            if (-not (Get-MarkdownAnchors $path).Contains($anchor)) {
                $failures.Add("Missing heading: $($file.Name) -> $target")
            }
        }
    }
}

foreach ($original in Get-ChildItem -LiteralPath (Join-Path $documentRoot 'docs') -Filter '*.md' -File) {
    $english = Join-Path $documentRoot ('docs\en\' + $original.Name)
    if (-not (Test-Path -LiteralPath $english -PathType Leaf)) { $failures.Add("Missing English page: $($original.Name)"); continue }
    $idBody = Get-Content -LiteralPath $original.FullName -Raw -Encoding UTF8
    $enBody = Get-Content -LiteralPath $english -Raw -Encoding UTF8
    if (-not $idBody.Contains('[English](en/' + $original.Name + ')') -or
        -not $enBody.Contains('[Bahasa Indonesia](../' + $original.Name + ')')) {
        $failures.Add("Missing reciprocal language navigation: $($original.Name)")
    }
}
if ($failures.Count) { throw ($failures -join [Environment]::NewLine) }
Write-Host "Documentation checks passed: $($files.Count) pages, $linkCount local file/heading links, and reciprocal guide languages."
