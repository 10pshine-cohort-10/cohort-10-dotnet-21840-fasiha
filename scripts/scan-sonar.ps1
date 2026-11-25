$ErrorActionPreference = 'Stop'
foreach ($name in @('SONAR_HOST_URL', 'SONAR_PROJECT_KEY', 'SONAR_TOKEN')) {
    if (-not [Environment]::GetEnvironmentVariable($name)) {
        throw "Set $name before running the SonarQube scan."
    }
}

function Invoke-Dotnet {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]] $Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments[0]) exited with code $LASTEXITCODE."
    }
}

Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    Invoke-Dotnet tool restore
    Invoke-Dotnet sonarscanner begin "/k:$env:SONAR_PROJECT_KEY" `
        "/d:sonar.host.url=$env:SONAR_HOST_URL" "/d:sonar.token=$env:SONAR_TOKEN" `
        '/d:sonar.scanner.scanAll=true' '/d:sonar.qualitygate.wait=true' `
        '/d:sonar.exclusions=**/node_modules/**,**/dist/**,**/Data/Migrations/**,**/package-lock.json' `
        '/d:sonar.cs.opencover.reportsPaths=tests/**/TestResults/**/coverage.opencover.xml'

    Invoke-Dotnet build TaskManagement.slnx -c Release --no-incremental
    Invoke-Dotnet -Arguments @(
        'test', 'TaskManagement.slnx', '-c', 'Release', '--no-build', '--collect', 'XPlat Code Coverage', '--',
        'DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover'
    )
    Invoke-Dotnet sonarscanner end "/d:sonar.token=$env:SONAR_TOKEN"
}
finally {
    Pop-Location
}
