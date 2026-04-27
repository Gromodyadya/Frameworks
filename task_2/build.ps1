$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$env:DOTNET_CLI_HOME = $root
$env:APPDATA = Join-Path $root ".appdata"
$env:NUGET_PACKAGES = Join-Path $root ".nuget\packages"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
New-Item -ItemType Directory -Force -Path (Join-Path $env:APPDATA "NuGet") | Out-Null
Copy-Item (Join-Path $root "NuGet.Config") (Join-Path $env:APPDATA "NuGet\NuGet.Config") -Force

$projects = @(
    "src\Task2.Contracts\Task2.Contracts.csproj",
    "src\Task2.Core\Task2.Core.csproj",
    "modules-src\Task2.ValidationModule\Task2.ValidationModule.csproj",
    "modules-src\Task2.ReportingModule\Task2.ReportingModule.csproj",
    "modules-src\Task2.ExportModule\Task2.ExportModule.csproj",
    "src\Task2.Host\Task2.Host.csproj",
    "tests\Task2.Tests\Task2.Tests.csproj"
)

foreach ($project in $projects) {
    dotnet build (Join-Path $root $project) --configuration Release --configfile (Join-Path $root "NuGet.Config")
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$target = Join-Path $root "src\Task2.Host\modules"
New-Item -ItemType Directory -Force -Path $target | Out-Null
Get-ChildItem (Join-Path $root "modules-src") -Recurse -Filter "Task2.*Module.dll" |
    Where-Object { $_.FullName -like "*\bin\Release\net8.0\*" } |
    Copy-Item -Destination $target -Force

Write-Host "Modules copied to $target"
