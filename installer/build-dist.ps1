# Publishes self-contained win-x64 builds and packages them into ..\dist
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dist = Join-Path $root 'dist'
$pub = Join-Path $dist 'publish'
if (Test-Path $pub) { Remove-Item $pub -Recurse -Force }
dotnet publish "$root\src\MatRewrite.Repl" -c Release -r win-x64 --self-contained true -o "$pub\matrw" -p:PublishSingleFile=false | Out-Host
dotnet publish "$root\src\MatRewrite.Ide" -c Release -r win-x64 --self-contained true -o "$pub\ide" | Out-Host
$zipDir = Join-Path $dist 'MatOct-portable'
if (Test-Path $zipDir) { Remove-Item $zipDir -Recurse -Force }
New-Item -ItemType Directory $zipDir | Out-Null
Copy-Item "$pub\ide\*" $zipDir -Recurse
New-Item -ItemType Directory "$zipDir\cli", "$zipDir\demo" | Out-Null
Copy-Item "$pub\matrw\*" "$zipDir\cli" -Recurse
Copy-Item "$pub\matrw\matrw.exe" "$zipDir\cli\matoct.exe"
Copy-Item "$root\demo\ide_demo.m" "$zipDir\demo"
Copy-Item "$PSScriptRoot\install-portable.ps1", "$PSScriptRoot\uninstall-portable.ps1" $zipDir
$zip = Join-Path $dist 'MatOct-0.22.0-win-x64-portable.zip'
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path "$zipDir\*" -DestinationPath $zip
$iscc = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if ($iscc) { & $iscc "$PSScriptRoot\MatOct.iss" | Out-Host } else { Write-Host 'Inno Setup not found: portable zip only' }
