param(
    [switch]$SelfContained = $true,
    [string]$Rid = "win-x64",
    [string]$Configuration = "Release",
    [switch]$Restore
)

$ErrorActionPreference = "Stop"

if ($Restore) {
    Write-Host "Restoring NuGet packages (ignoring failed sources)..." -ForegroundColor Cyan
    dotnet restore ..\Redbright.sln --ignore-failed-sources
}

Write-Host "Publishing Redbright.App ($Configuration, RID=$Rid, SelfContained=$SelfContained)..." -ForegroundColor Cyan
$publishArgs = @("publish", "..\Redbright.App\Redbright.App.csproj", "-c", $Configuration, "-r", $Rid, "/p:PublishSingleFile=true", "/p:PublishTrimmed=false")
if ($SelfContained) { $publishArgs += "/p:SelfContained=true" } else { $publishArgs += "/p:SelfContained=false" }
if (-not $Restore) { $publishArgs += "--no-restore" }
dotnet @publishArgs

$publishDir = Join-Path -Path (Resolve-Path "..\Redbright.App\bin\$Configuration\net8.0-windows\$Rid").Path -ChildPath "publish"
if (!(Test-Path $publishDir)) {
    throw "Publish directory not found: $publishDir"
}
Write-Host "Publish directory: $publishDir" -ForegroundColor Green

$issPath = Resolve-Path ".\Redbright.iss"
if (-not (Get-Command iscc.exe -ErrorAction SilentlyContinue)) {
    Write-Warning "Inno Setup compiler (iscc.exe) not found in PATH. Skipping installer compile."
    Write-Host "You can compile manually in Inno Setup using installer\Redbright.iss" -ForegroundColor Yellow
    exit 0
}

Write-Host "Compiling Inno Setup installer..." -ForegroundColor Cyan
# Derive architecture tag from RID (e.g., win-x64 -> x64)
$arch = "x64"
if ($Rid -match 'win-(.+)$') { $arch = $Matches[1] }
& iscc.exe $issPath /DMyArch=$arch
Write-Host "Installer build finished." -ForegroundColor Green


# Generate SHA-256 checksum for the installer using certutil
$issDir = Split-Path -Path $issPath -Parent
$outputDir = Join-Path -Path $issDir -ChildPath "Output"
# Determine app name and version from the Inno Setup script
$nameMatch = Select-String -Path $issPath -Pattern '^\s*#define\s+MyAppName\s+"([^"]+)"'
$versionMatch = Select-String -Path $issPath -Pattern '^\s*#define\s+MyAppVersion\s+"([^"]+)"'
$appName = if ($nameMatch) { $nameMatch.Matches[0].Groups[1].Value } else { "Redbright" }
$appVersion = if ($versionMatch) { $versionMatch.Matches[0].Groups[1].Value } else { "0.0.0" }
$installerFileName = "$appName-$appVersion-$arch-Setup.exe"
$installerPath = Join-Path -Path $outputDir -ChildPath $installerFileName
# Fallback to legacy filename if needed
if (!(Test-Path $installerPath)) {
    $fallback1 = Join-Path -Path $outputDir -ChildPath "$appName-$appVersion-Setup.exe"
    if (Test-Path $fallback1) {
        $installerPath = $fallback1
    } else {
        $installerPath = Join-Path -Path $outputDir -ChildPath "Redbright-Setup.exe"
    }
}
if (Test-Path $installerPath) {
    Write-Host "Generating SHA-256 checksum for $(Split-Path -Leaf $installerPath)..." -ForegroundColor Cyan
    try {
        $checksumPath = "$installerPath.sha256.txt"
        certutil -hashfile $installerPath SHA256 | Set-Content -Path $checksumPath
        Write-Host "Wrote checksum file: $checksumPath" -ForegroundColor Green
    } catch {
        Write-Warning "Failed to generate SHA-256 checksum: $($_.Exception.Message)"
    }
} else {
    Write-Warning "Installer not found at: $installerPath. Skipping checksum generation."
}
