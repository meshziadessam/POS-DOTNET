param([string]$Configuration = 'Release', [string]$InnoCompiler = '', [switch]$BundleFramework)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    & dotnet build RestaurantPos.sln -c $Configuration -t:Rebuild -p:PlatformTarget=AnyCPU
    if ($LASTEXITCODE -ne 0) { throw 'Solution build failed' }
    if ($InnoCompiler) {
        if ($Configuration -ne 'Release') { throw 'Installer packaging requires Release configuration' }
        if ($BundleFramework) {
            $prerequisite = Join-Path $PSScriptRoot '.tools/ndp48-x86-x64-allos-enu.exe'
            $signature = Get-AuthenticodeSignature -LiteralPath $prerequisite
            if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Microsoft Corporation') { throw 'Offline Framework prerequisite is missing or its Microsoft signature is invalid' }
            & $InnoCompiler /DBundleFramework=1 installer/RestaurantPos.iss
        } else {
            & $InnoCompiler installer/RestaurantPos.iss
        }
        if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }
    }
} finally { Pop-Location }
