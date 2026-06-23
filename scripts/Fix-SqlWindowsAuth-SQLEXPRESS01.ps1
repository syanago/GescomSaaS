param(
    [string]$InstanceName = "SQLEXPRESS01",
    [int]$TcpPort = 14333
)

$ErrorActionPreference = "Stop"

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Ce script doit etre execute en tant qu'Administrateur."
    }
}

function Set-RegistryValueIfDifferent {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)]$Value
    )

    $current = (Get-ItemProperty -Path $Path -Name $Name -ErrorAction SilentlyContinue).$Name
    if ($current -ne $Value) {
        Set-ItemProperty -Path $Path -Name $Name -Value $Value
    }
}

Assert-Administrator

$instanceRegistryName = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL').$InstanceName
if (-not $instanceRegistryName) {
    throw "Instance SQL '$InstanceName' introuvable dans le registre."
}

$baseKey = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\$instanceRegistryName\MSSQLServer\SuperSocketNetLib"
$npKey = Join-Path $baseKey "Np"
$tcpKey = Join-Path $baseKey "Tcp"
$ipAllKey = Join-Path $tcpKey "IPAll"
$ipLoopbackKey = Join-Path $tcpKey "IP10"

Write-Host "Activation des protocoles reseau pour $InstanceName..."
Set-RegistryValueIfDifferent -Path $npKey -Name "Enabled" -Value 1
Set-RegistryValueIfDifferent -Path $tcpKey -Name "Enabled" -Value 1

if (Test-Path $ipLoopbackKey) {
    Set-RegistryValueIfDifferent -Path $ipLoopbackKey -Name "Enabled" -Value 1
}

Set-RegistryValueIfDifferent -Path $ipAllKey -Name "TcpDynamicPorts" -Value ""
Set-RegistryValueIfDifferent -Path $ipAllKey -Name "TcpPort" -Value "$TcpPort"

$sqlService = "MSSQL`$$InstanceName"
Write-Host "Configuration du service SQL Browser..."
Set-Service -Name "SQLBrowser" -StartupType Automatic
Start-Service -Name "SQLBrowser"

Write-Host "Redemarrage du service $sqlService..."
Restart-Service -Name $sqlService -Force

Start-Sleep -Seconds 8

Write-Host ""
Write-Host "Verification des services :"
Get-Service -Name $sqlService, "SQLBrowser" | Select-Object Name, Status, StartType | Format-Table -AutoSize

Write-Host ""
Write-Host "Verification des endpoints :"
Get-ItemProperty $npKey | Select-Object Enabled, PipeName | Format-List
Get-ItemProperty $ipAllKey | Select-Object TcpDynamicPorts, TcpPort | Format-List

Write-Host ""
Write-Host "Netstat sur le port $TcpPort :"
netstat -ano | Select-String ":$TcpPort"

Write-Host ""
Write-Host "Correctif termine."
Write-Host "Si LigCom continue a viser l'instance nommee, le Browser devrait suffire."
Write-Host "Sinon, tu peux utiliser temporairement cette chaine Windows auth dans l'application :"
Write-Host "Server=localhost,$TcpPort;Database=GescomSaas;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=False"
