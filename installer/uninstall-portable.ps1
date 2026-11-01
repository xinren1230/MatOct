param([string]$Target = "$env:LOCALAPPDATA\Programs\MatRewrite")
Remove-Item (Join-Path ([Environment]::GetFolderPath('Programs')) 'MatRewrite') -Recurse -Force -ErrorAction SilentlyContinue
if ((Get-ItemProperty 'HKCU:\Software\Classes\.m' -ErrorAction SilentlyContinue).'(default)' -eq 'MatRewrite.MFile') { Remove-Item 'HKCU:\Software\Classes\.m' -Recurse -Force }
Remove-Item 'HKCU:\Software\Classes\MatRewrite.MFile' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $Target -Recurse -Force -ErrorAction SilentlyContinue
