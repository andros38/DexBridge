param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$Artifacts = Join-Path $ProjectRoot "artifacts"
$BundledRuntime = Join-Path $ProjectRoot "runtime\scrcpy"
$PublishTemp = Join-Path $Artifacts "publish-temp"
[xml]$ProjectMetadata = Get-Content (Join-Path $ProjectRoot "DexBridge.csproj")
$AppVersion = [string]$ProjectMetadata.Project.PropertyGroup.Version
$PackageRoot = Join-Path $Artifacts "DexBridge-v$AppVersion-win64"
$ZipOutput = Join-Path $Artifacts "DexBridge-v$AppVersion-win64.zip"

$scrcpyExe = Join-Path $BundledRuntime "scrcpy.exe"
$adbExe = Join-Path $BundledRuntime "adb.exe"
$scrcpyServer = Join-Path $BundledRuntime "scrcpy-server"

foreach ($required in @($scrcpyExe, $adbExe, $scrcpyServer)) {
    if (-not (Test-Path $required)) {
        throw "Bundled runtime is incomplete: missing $required"
    }
}

New-Item -ItemType Directory -Force -Path $Artifacts | Out-Null
Remove-Item -Recurse -Force $PublishTemp -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force $PackageRoot -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $PublishTemp, $PackageRoot | Out-Null

Write-Host "Publishing DexBridge v$AppVersion as a self-contained Windows application..."
dotnet publish (Join-Path $ProjectRoot "DexBridge.csproj") `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -p:DebugType=None `
    -o $PublishTemp
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

Copy-Item -Path (Join-Path $PublishTemp "*") -Destination $PackageRoot -Recurse -Force

# Copy scrcpy/ADB from the checked-in runtime explicitly. The .NET publish output
# does not reliably preserve the runtime/scrcpy subtree on every SDK/environment.
# The app resolves scrcpy.exe and adb.exe relative to the executable at this path.
$RuntimeOutput = Join-Path $PackageRoot "runtime\scrcpy"
New-Item -ItemType Directory -Force -Path $RuntimeOutput | Out-Null
Get-ChildItem -LiteralPath $BundledRuntime -Force | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $RuntimeOutput -Recurse -Force
}
Write-Host "Bundled scrcpy/ADB copied to $RuntimeOutput"

Copy-Item (Join-Path $ProjectRoot "docs\PANDUAN-PENGGUNA.md") (Join-Path $PackageRoot "BACA-SAYA.md") -Force
New-Item -ItemType Directory -Force -Path (Join-Path $PackageRoot "licenses") | Out-Null
if (Test-Path (Join-Path $BundledRuntime "LICENSE.txt")) {
    Copy-Item (Join-Path $BundledRuntime "LICENSE.txt") (Join-Path $PackageRoot "licenses\scrcpy-LICENSE.txt") -Force
}
if (Test-Path (Join-Path $ProjectRoot "licenses\THIRD-PARTY-NOTICES.txt")) {
    Copy-Item (Join-Path $ProjectRoot "licenses\THIRD-PARTY-NOTICES.txt") (Join-Path $PackageRoot "licenses\THIRD-PARTY-NOTICES.txt") -Force
}

# Verify the actual distribution contents, not just the intermediate publish output.
# scrcpy-server and its DLL dependencies must stay beside scrcpy.exe.
$requiredPublishedFiles = @(
    "DexBridge.exe",
    "runtime\scrcpy\scrcpy.exe",
    "runtime\scrcpy\adb.exe",
    "runtime\scrcpy\scrcpy-server",
    "runtime\scrcpy\SDL3.dll"
)
foreach ($relativeFile in $requiredPublishedFiles) {
    $publishedFile = Join-Path $PackageRoot $relativeFile
    if (-not (Test-Path -LiteralPath $publishedFile -PathType Leaf)) {
        throw "Portable package is missing required file: $publishedFile"
    }
}

Remove-Item $ZipOutput -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $PackageRoot "*") -DestinationPath $ZipOutput -CompressionLevel Optimal

Write-Host ""
Write-Host "Portable package created:" -ForegroundColor Green
Write-Host "  $ZipOutput"
Write-Host "Users do not need .NET, scrcpy, ADB, or Android SDK installed separately."
