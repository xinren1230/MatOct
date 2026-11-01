# Per-user install of the portable package: copies files, adds Start menu shortcuts and the .m file association (HKCU, no admin).
param([string]$Target = "$env:LOCALAPPDATA\Programs\MatRewrite")
$ErrorActionPreference = 'Stop'
$src = $PSScriptRoot
New-Item -ItemType Directory -Force $Target | Out-Null
Copy-Item "$src\*" $Target -Recurse -Force
$ide = Join-Path $Target 'MatRewriteIde.exe'
$menu = Join-Path ([Environment]::GetFolderPath('Programs')) 'MatRewrite'
New-Item -ItemType Directory -Force $menu | Out-Null
$sh = New-Object -ComObject WScript.Shell
foreach ($p in @(@('MatRewrite IDE.lnk', $ide), @('MatRewrite Console.lnk', (Join-Path $Target 'cli\matrw.exe')))) {
  $l = $sh.CreateShortcut((Join-Path $menu $p[0])); $l.TargetPath = $p[1]; $l.WorkingDirectory = $Target; $l.Save()
}
$cls = 'HKCU:\Software\Classes'
New-Item -Force "$cls\.m" | Out-Null; Set-ItemProperty "$cls\.m" '(default)' 'MatRewrite.MFile'
New-Item -Force "$cls\MatRewrite.MFile\DefaultIcon" | Out-Null; Set-ItemProperty "$cls\MatRewrite.MFile" '(default)' 'MatRewrite script'
Set-ItemProperty "$cls\MatRewrite.MFile\DefaultIcon" '(default)' "$ide,0"
New-Item -Force "$cls\MatRewrite.MFile\shell\open\command" | Out-Null
Set-ItemProperty "$cls\MatRewrite.MFile\shell\open\command" '(default)' "`"$ide`" `"%1`""
Write-Host "Installed to $Target"
