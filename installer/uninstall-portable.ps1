param([string]$Target = "$env:LOCALAPPDATA\Programs\MatOct")
Remove-Item (Join-Path ([Environment]::GetFolderPath('Programs')) 'MatOct') -Recurse -Force -ErrorAction SilentlyContinue
if ((Get-ItemProperty 'HKCU:\Software\Classes\.m' -ErrorAction SilentlyContinue).'(default)' -eq 'MatOct.MFile') { Remove-Item 'HKCU:\Software\Classes\.m' -Recurse -Force }
Remove-Item 'HKCU:\Software\Classes\MatOct.MFile' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $Target -Recurse -Force -ErrorAction SilentlyContinue
