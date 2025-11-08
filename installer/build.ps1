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
& iscc.exe $issPath
Write-Host "Installer build finished." -ForegroundColor Green


