; MatRewrite installer (Inno Setup 6). Build: ISCC.exe MatRewrite.iss  (run build-dist.ps1 first)
#define AppName "MatRewrite"
#define AppVer "0.9.0"
[Setup]
AppId={{6C1E0B7A-9A43-4D7E-9B0E-4D61A6B2F909}
AppName={#AppName}
AppVersion={#AppVer}
AppPublisher=Xinren Chen
DefaultDirName={localappdata}\Programs\MatRewrite
DefaultGroupName=MatRewrite
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=MatRewrite-{#AppVer}-win-x64-setup
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
ChangesAssociations=yes
[Files]
Source: "..\dist\publish\ide\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion
Source: "..\dist\publish\matrw\*"; DestDir: "{app}\cli"; Flags: recursesubdirs ignoreversion
Source: "..\demo\ide_demo.m"; DestDir: "{app}\demo"
[Icons]
Name: "{group}\MatRewrite IDE"; Filename: "{app}\MatRewriteIde.exe"
Name: "{group}\MatRewrite Console"; Filename: "{app}\cli\matrw.exe"
Name: "{group}\Uninstall MatRewrite"; Filename: "{uninstallexe}"
[Registry]
Root: HKCU; Subkey: "Software\Classes\.m"; ValueType: string; ValueName: ""; ValueData: "MatRewrite.MFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\MatRewrite.MFile"; ValueType: string; ValueName: ""; ValueData: "MatRewrite script"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\MatRewrite.MFile\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\MatRewriteIde.exe,0"
Root: HKCU; Subkey: "Software\Classes\MatRewrite.MFile\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\MatRewriteIde.exe"" ""%1"""
[Run]
Filename: "{app}\MatRewriteIde.exe"; Description: "Launch MatRewrite IDE"; Flags: nowait postinstall skipifsilent
