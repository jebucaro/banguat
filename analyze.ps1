#Requires -Version 7.3
# PowerShell equivalent of analyze.sh. Requires a running local SonarQube.

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

$envFile = Join-Path $PSScriptRoot '.env'

if (-not (Test-Path $envFile)) {
    Write-Host "Error: .env file not found at $envFile"
    Write-Host 'Copy .env.example to .env and fill in your SONAR_TOKEN.'
    exit 1
}

foreach ($line in Get-Content $envFile) {
    $trimmed = $line.Trim()
    if ($trimmed -eq '' -or $trimmed.StartsWith('#')) { continue }
    $name, $value = $trimmed -split '=', 2
    if ($null -eq $value) { continue }
    [Environment]::SetEnvironmentVariable($name.Trim(), $value.Trim().Trim('"').Trim("'"))
}

if (-not $env:SONAR_TOKEN) {
    Write-Host 'Error: SONAR_TOKEN is not set in .env'
    exit 1
}

$hostUrl = if ($env:SONAR_HOST_URL) { $env:SONAR_HOST_URL } else { 'http://localhost:9000' }

Push-Location $PSScriptRoot
try {
    # Skip the Husky hook-install MSBuild target during restore/build; this is an analysis run, not a commit workflow.
    $env:HUSKY = '0'

    dotnet tool restore

    Get-ChildItem -Path 'tests' -Directory -Recurse -Filter 'TestResults' |
        Remove-Item -Recurse -Force

    dotnet tool run dotnet-sonarscanner begin `
        /k:"banguat-exchangerates" `
        /d:sonar.host.url="$hostUrl" `
        /d:sonar.token="$env:SONAR_TOKEN" `
        /d:sonar.cs.opencover.reportsPaths="tests/**/coverage.opencover.xml" `
        /d:sonar.exclusions="**/obj/**,**/bin/**"

    dotnet build --no-incremental --disable-build-servers

    # coverage.runsettings replaces each project's own runsettings, so it repeats the filter that keeps the live
    # Banguat integration test out of the run.
    dotnet test --no-build --collect:"XPlat Code Coverage" --settings coverage.runsettings

    dotnet tool run dotnet-sonarscanner end /d:sonar.token="$env:SONAR_TOKEN"
}
finally {
    Pop-Location
}
