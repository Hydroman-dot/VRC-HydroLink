$ErrorActionPreference = 'Stop'
$required = @(
    'LICENSE', 'THIRD-PARTY-NOTICES.md',
    'licenses/VR-Fitness-MIT.txt', 'licenses/VRC-Windforce-MIT.txt',
    'licenses/OSCQuery-MIT.txt', 'licenses/MeaMod.DNS-MIT.txt',
    'licenses/Buttplug.txt', 'licenses/MQTTnet.txt',
    'licenses/Newtonsoft.Json.txt', 'licenses/Microsoft-Logging-MIT.txt',
    'licenses/Microsoft-ProtectedData.txt', 'licenses/dotnet-LICENSE.txt',
    'licenses/dotnet-ThirdPartyNotices.txt', 'licenses/CsWinRT-MIT.txt',
    'licenses/Windows-SDK-License.rtf'
)
foreach ($relative in $required) {
    $file = Get-Item -LiteralPath (Join-Path $PSScriptRoot $relative) -ErrorAction Stop
    if ($file.PSIsContainer -or $file.Length -lt 100) {
        throw "Missing or empty license document: $relative"
    }
}
Write-Host "License packaging check passed: $($required.Count) documents. This checks presence, not legal clearance."
