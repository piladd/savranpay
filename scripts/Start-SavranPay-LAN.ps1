$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$ip = (Get-NetIPAddress -AddressFamily IPv4 |
    Where-Object {
        $_.IPAddress -notlike "127.*" -and
        $_.IPAddress -notlike "169.254.*" -and
        $_.PrefixOrigin -ne "WellKnown"
    } |
    Select-Object -First 1 -ExpandProperty IPAddress)

Write-Host "Starting SavranPay LAN profile..."
Write-Host "Open on this computer: http://localhost:5080"
if ($ip) {
    Write-Host "Open from another device on the same Wi-Fi/LAN: http://${ip}:5080"
}

dotnet run --project src\SavranPay.Api\SavranPay.Api.csproj --launch-profile lan-http
