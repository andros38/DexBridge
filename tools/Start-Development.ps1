param([switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Tidak ada dotnet di PATH. Instal .NET 10 SDK, kemudian buka terminal baru.'
}
$ver = & dotnet --version
if ($LASTEXITCODE -ne 0) { throw 'Gagal menjalankan dotnet --version.' }
Write-Host "Menggunakan .NET SDK $ver"
foreach ($name in @('scrcpy.exe', 'adb.exe', 'scrcpy-server')) {
    if (-not (Test-Path (Join-Path $repo "runtime\scrcpy\$name"))) {
        throw "Runtime belum lengkap: runtime\scrcpy\$name"
    }
}
Set-Location $repo
if (-not $NoBuild) {
    & dotnet restore (Join-Path $repo 'DexBridge.csproj')
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore gagal.' }
}
& dotnet run --project (Join-Path $repo 'DexBridge.csproj') -c Debug --no-restore
if ($LASTEXITCODE -ne 0) { throw "DexBridge ditutup dengan exit code $LASTEXITCODE" }
