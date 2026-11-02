; MatOct installer (Inno Setup 6). Build: ISCC.exe MatOct.iss  (run build-dist.ps1 first)
#define AppName "MatOct"
#define AppVer "0.17.0"
[Setup]
AppId={{8F3D2C51-7B1A-4E0C-A9D2-5E1F0C3B7A12}
UsePreviousAppDir=no
AppName={#AppName}
AppVersion={#AppVer}
AppPublisher=Xinren Chen
DefaultDirName={localappdata}\Programs\MatOct
DefaultGroupName=MatOct
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=MatOct-{#AppVer}-win-x64-setup
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
ChangesAssociations=yes
ChangesEnvironment=yes
SetupIconFile=app.ico
UninstallDisplayIcon={app}\MatOct.exe
[Files]
Source: "..\dist\publish\ide\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion
Source: "..\dist\publish\matrw\*"; DestDir: "{app}\cli"; Flags: recursesubdirs ignoreversion
Source: "..\dist\publish\matrw\matrw.exe"; DestDir: "{app}\cli"; DestName: "matoct.exe"; Flags: ignoreversion
Source: "..\demo\ide_demo.m"; DestDir: "{app}\demo"
[Icons]
Name: "{group}\MatOct"; Filename: "{app}\MatOct.exe"
Name: "{group}\MatOct Console"; Filename: "{app}\cli\matrw.exe"
Name: "{group}\Uninstall MatOct"; Filename: "{uninstallexe}"
[Registry]
Root: HKCU; Subkey: "Software\Classes\.m"; ValueType: string; ValueName: ""; ValueData: "MatOct.MFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\MatOct.MFile"; ValueType: string; ValueName: ""; ValueData: "MatOct script"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\MatOct.MFile\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\MatOct.exe,0"
Root: HKCU; Subkey: "Software\Classes\MatOct.MFile\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\MatOct.exe"" ""%1"""
Root: HKCU; Subkey: "Environment"; ValueType: expandsz; ValueName: "Path"; ValueData: "{olddata};{app}\cli"; Check: NeedsAddPath(ExpandConstant('{app}\cli'))
[Run]
Filename: "{app}\MatOct.exe"; Description: "Launch MatOct"; Flags: nowait postinstall skipifsilent
[Code]
function NeedsAddPath(Dir: string): Boolean;
var P: string;
begin
  if not RegQueryStringValue(HKCU, 'Environment', 'Path', P) then begin Result := True; exit; end;
  Result := Pos(';' + Lowercase(Dir) + ';', ';' + Lowercase(P) + ';') = 0;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var P, Dir: string; I: Integer;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    Dir := ExpandConstant('{app}\cli');
    if RegQueryStringValue(HKCU, 'Environment', 'Path', P) then
    begin
      P := ';' + P + ';';
      I := Pos(';' + Lowercase(Dir) + ';', Lowercase(P));
      if I > 0 then
      begin
        Delete(P, I, Length(Dir) + 1);
        P := Copy(P, 2, Length(P) - 2);
        RegWriteExpandStringValue(HKCU, 'Environment', 'Path', P);
      end;
    end;
  end;
end;
