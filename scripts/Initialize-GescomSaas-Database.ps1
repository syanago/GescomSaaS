param(
    [string]$SqlServerInstance = "DESKTOP-MFILR2N\SQLEXPRESS01",
    [string]$DatabaseName = "GescomSaas",
    [switch]$DropExisting
)

$ErrorActionPreference = "Stop"

$workspaceRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $workspaceRoot "src\GescomSaas.Web\GescomSaas.Web.csproj"

$connectionString = "Server=$SqlServerInstance;Database=$DatabaseName;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"

Write-Host "Instance SQL Server : $SqlServerInstance"
Write-Host "Base cible         : $DatabaseName"
Write-Host "Projet             : $projectPath"

if ($DropExisting) {
    Write-Host "Suppression de la base existante..."
}

$env:ConnectionStrings__DefaultConnection = $connectionString

Write-Host "Generation du schema et des donnees de demonstration..."
if ($DropExisting) {
    & dotnet run --project $projectPath -- --drop-database --seed-only
}
else {
    & dotnet run --project $projectPath -- --seed-only
}
if ($LASTEXITCODE -ne 0) {
    throw "La generation du schema/de la demo a echoue (code $LASTEXITCODE)."
}

Write-Host ""
Write-Host "Base generee avec succes."
Write-Host "Connection string : $connectionString"
