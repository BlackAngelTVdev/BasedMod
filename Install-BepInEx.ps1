#Requires -Version 5.1
<#
    Installe BepInEx 5.4.23.5 (x64) dans Supermarket Together.
    Usage :
        powershell -ExecutionPolicy Bypass -File .\Install-BepInEx.ps1
        powershell -ExecutionPolicy Bypass -File .\Install-BepInEx.ps1 -GamePath "D:\SteamLibrary\steamapps\common\Supermarket Together"
#>
param(
    [string]$GamePath = "E:\SteamLibrary\steamapps\common\Supermarket Together",
    [string]$BepInExVersion = "5.4.23.5"
)

$ErrorActionPreference = "Stop"

$exe = Join-Path $GamePath "Supermarket Together.exe"
if (-not (Test-Path $exe)) {
    throw "Supermarket Together.exe introuvable dans '$GamePath'. Corrige -GamePath."
}

$coreDll = Join-Path $GamePath "BepInEx\core\BepInEx.dll"
if (Test-Path $coreDll) {
    Write-Host "BepInEx est déjà installé dans '$GamePath'."
    exit 0
}

$zipName = "BepInEx_win_x64_$BepInExVersion.zip"
$url = "https://github.com/BepInEx/BepInEx/releases/download/v$BepInExVersion/$zipName"
$zipPath = Join-Path $env:TEMP $zipName

Write-Host "Téléchargement de $url ..."
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Invoke-WebRequest -Uri $url -OutFile $zipPath -UseBasicParsing

Write-Host "Extraction dans '$GamePath' ..."
Expand-Archive -Path $zipPath -DestinationPath $GamePath -Force
Remove-Item $zipPath -Force

New-Item -ItemType Directory -Force -Path (Join-Path $GamePath "BepInEx\plugins") | Out-Null

if (-not (Test-Path $coreDll)) {
    throw "Installation incomplète : BepInEx.dll absent de BepInEx\core."
}

Write-Host "OK : BepInEx $BepInExVersion installé dans '$GamePath'."
