# Per-user install of the portable package: copies files, adds Start menu shortcuts and the .m file association (HKCU, no admin).
param([string]$Target = "$env:LOCALAPPDATA\Programs\MatOct")
$ErrorActionPreference = 'Stop'
$src = $PSScriptRoot
New-Item -ItemType Directory -Force $Target | Out-Null
Copy-Item "$src\*" $Target -Recurse -Force
$ide = Join-Path $Target 'MatOct.exe'
$menu = Join-Path ([Environment]::GetFolderPath('Programs')) 'MatOct'
New-Item -ItemType Directory -Force $menu | Out-Null
$sh = New-Object -ComObject WScript.Shell
foreach ($p in @(@('MatOct.lnk', $ide), @('MatOct Console.lnk', (Join-Path $Target 'cli\matrw.exe')))) {
  $l = $sh.CreateShortcut((Join-Path $menu $p[0])); $l.TargetPath = $p[1]; $l.WorkingDirectory = $Target; $l.Save()
}
$cls = 'HKCU:\Software\Classes'
New-Item -Force "$cls\.m" | Out-Null; Set-ItemProperty "$cls\.m" '(default)' 'MatOct.MFile'
New-Item -Force "$cls\MatOct.MFile\DefaultIcon" | Out-Null; Set-ItemProperty "$cls\MatOct.MFile" '(default)' 'MatOct script'
Set-ItemProperty "$cls\MatOct.MFile\DefaultIcon" '(default)' "$ide,0"
New-Item -Force "$cls\MatOct.MFile\shell\open\command" | Out-Null
Set-ItemProperty "$cls\MatOct.MFile\shell\open\command" '(default)' "`"$ide`" `"%1`""
Write-Host "Installed to $Target"
