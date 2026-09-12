function Assert-CrossMacroOutputDirectory {
    param(
        [Parameter(Mandatory)][string]$Directory,
        [Parameter(Mandatory)][string]$RepositoryRoot
    )

    $candidate = [System.IO.Path]::GetFullPath($Directory)
    $root = [System.IO.Path]::GetFullPath($RepositoryRoot)
    $separator = [System.IO.Path]::DirectorySeparatorChar
    $comparison = [System.StringComparison]::OrdinalIgnoreCase
    $trimmed = $candidate.TrimEnd($separator)
    if ($trimmed -eq '' -or $candidate -eq [System.IO.Path]::GetPathRoot($candidate) -or
        $trimmed -eq $root.TrimEnd($separator) -or $trimmed -eq $HOME.TrimEnd($separator) -or
        $root.StartsWith($trimmed + $separator, $comparison)) {
        throw "Unsafe output directory contains the repository or is a filesystem/home root: $candidate"
    }

    if ($candidate.StartsWith($root.TrimEnd($separator) + $separator, $comparison)) {
        $relative = [System.IO.Path]::GetRelativePath($root, $candidate)
        $first = $relative.Split($separator)[0]
        if ($first -ne 'artifacts' -and $first -ne 'publish' -and $first -notlike 'publish-*') {
            throw "Output directory overlaps repository sources: $candidate"
        }
    }

    # Refuse symlink/junction ancestors before recursive deletion or staging.
    $cursor = $candidate
    while ($cursor) {
        if (Test-Path -LiteralPath $cursor) {
            $item = Get-Item -LiteralPath $cursor -Force
            if ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
                throw "Output directory traverses a symbolic link or junction: $cursor"
            }
        }
        $cursor = Split-Path -Parent $cursor
    }
}
