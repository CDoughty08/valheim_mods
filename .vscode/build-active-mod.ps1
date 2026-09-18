param(
    [Parameter(Mandatory = $true)]
    [string]$WorkspaceFolder,

    [Parameter(Mandatory = $false)]
    [string]$FilePath = ""
)

$ErrorActionPreference = "Stop"

function Find-CsprojWalkingUp([string]$StartDir, [string]$StopAt) {
    $cur = $StartDir
    while ($cur) {
        $proj = Get-ChildItem -LiteralPath $cur -Filter "*.csproj" -File -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($proj) {
            return $proj.FullName
        }
        if ($StopAt -and ($cur -eq $StopAt)) {
            break
        }
        $parent = Split-Path -Path $cur -Parent
        if (-not $parent -or $parent -eq $cur) {
            break
        }
        $cur = $parent
    }
    return $null
}

$ws = [System.IO.Path]::GetFullPath($WorkspaceFolder.TrimEnd("\", "/"))
$found = $null

if ($FilePath) {
    $file = [System.IO.Path]::GetFullPath($FilePath)
    $dir = Split-Path -Path $file -Parent
    $found = Find-CsprojWalkingUp -StartDir $dir -StopAt $ws

    # e.g. .../valheim/VariaTracking/Plugin.cs -> VariaTracking/VariaTracking.csproj
    if (-not $found -and $file.StartsWith($ws, [System.StringComparison]::OrdinalIgnoreCase)) {
        $rel = $file.Substring($ws.Length).TrimStart("\", "/")
        $mod = ($rel -split "[\\/]")[0]
        if ($mod -and $mod -ne ".") {
            $candidate = Join-Path $ws (Join-Path $mod "$mod.csproj")
            if (Test-Path -LiteralPath $candidate) {
                $found = $candidate
            }
        }
    }
}

if (-not $found) {
    Write-Error "Open a source file under a mod folder (e.g. VariaFood), or run task 'build all mods'."
    exit 1
}

Write-Host "Building $found"
& dotnet build $found -c Release
exit $LASTEXITCODE
