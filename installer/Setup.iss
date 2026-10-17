#ifndef ProductVersion
  #define ProductVersion "1.2.1"
#endif

[Setup]
AppId={{8E9C6C61-5A60-4EF0-AD84-8F59E877A663}
AppName=AstralParty.Toys
AppVersion={#ProductVersion}
AppPublisher=higashitaniyume
AppPublisherURL=https://github.com/higashitaniyume/AstralParty.Toys
DefaultDirName={localappdata}\Programs\AstralParty.Toys
DefaultGroupName=AstralParty.Toys
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDirectory}
OutputBaseFilename={#OutputBaseName}
SetupIconFile={#RepoRoot}\src\AstralParty.Toys\WebUI\images\UT_AccountHeadShot_70902.ico
UninstallDisplayIcon={app}\AstralParty.Toys.exe
LicenseFile={#RepoRoot}\LICENSE
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
DisableDirPage=no
UsePreviousAppDir=yes
UsePreviousTasks=yes
LanguageDetectionMethod=none
ShowLanguageDialog=no
CloseApplications=yes
RestartApplications=no
Uninstallable=yes
VersionInfoVersion={#ProductVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startmenuicon"; Description: "Create a Start menu shortcut"; GroupDescription: "Shortcuts:"
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDirectory}\*"; DestDir: "{app}"; Excludes: "LICENSE.txt"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#RepoRoot}\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\AstralParty.Toys\AstralParty.Toys"; Filename: "{app}\AstralParty.Toys.exe"; WorkingDir: "{app}"; Tasks: startmenuicon
Name: "{userdesktop}\AstralParty.Toys"; Filename: "{app}\AstralParty.Toys.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[InstallDelete]
Type: files; Name: "{userprograms}\AstralParty.Toys\AstralParty.Toys.lnk"; Check: RemoveStartMenuShortcut
Type: files; Name: "{userdesktop}\AstralParty.Toys.lnk"; Check: RemoveDesktopShortcut

[Code]
function VersionPart(var Value: String): Integer;
var
  Position: Integer;
  Part: String;
begin
  Position := Pos('.', Value);
  if Position = 0 then begin Part := Value; Value := ''; end
  else begin Part := Copy(Value, 1, Position - 1); Delete(Value, 1, Position); end;
  Result := StrToIntDef(Part, 0);
end;

function IsNewer(const InstalledVersion: String): Boolean;
var
  Existing, Incoming: String;
  I, A, B: Integer;
begin
  Existing := InstalledVersion;
  Incoming := '{#ProductVersion}';
  Result := False;
  for I := 1 to 4 do begin
    A := VersionPart(Existing); B := VersionPart(Incoming);
    if A > B then begin Result := True; Exit; end;
    if A < B then Exit;
  end;
end;

function InitializeSetup(): Boolean;
var
  ExistingVersion: String;
begin
  Result := True;
  if RegQueryStringValue(HKCU64, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{8E9C6C61-5A60-4EF0-AD84-8F59E877A663}_is1', 'DisplayVersion', ExistingVersion) then
    if IsNewer(ExistingVersion) then begin
      SuppressibleMsgBox('A newer version of AstralParty.Toys is already installed. Setup cannot downgrade it.', mbError, MB_OK, IDOK);
      Result := False;
    end;
end;

function RemoveStartMenuShortcut(): Boolean;
begin
  Result := not WizardIsTaskSelected('startmenuicon');
end;

function RemoveDesktopShortcut(): Boolean;
begin
  Result := not WizardIsTaskSelected('desktopicon');
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Directory, Probe: String;
  WasPresent: Boolean;
begin
  Result := True;
  if CurPageID <> wpSelectDir then Exit;
  Directory := WizardDirValue;
  WasPresent := DirExists(Directory);
  if not ForceDirectories(Directory) then begin
    MsgBox('This folder cannot be created with your current permissions. Choose another folder, or run Setup with the appropriate permissions.', mbError, MB_OK);
    Result := False; Exit;
  end;
  Probe := GenerateUniqueName(AddBackslash(Directory), '.toys-write-check.tmp');
  Result := SaveStringToFile(Probe, 'AstralParty.Toys installer permission check', False);
  if Result then Result := DeleteFile(Probe);
  if not WasPresent then RemoveDir(Directory);
  if not Result then
    MsgBox('This folder cannot be written to or cleaned up with your current permissions. Choose another folder.', mbError, MB_OK);
end;

function InitializeUninstall(): Boolean;
begin
  if UninstallSilent then begin Result := True; Exit; end;
  Result := MsgBox('Uninstalling AstralParty.Toys will NOT remove CesiumLoader (the Mod loader) or mods from your game folder.' + #13#10#13#10 + 'To remove the Mod loader, cancel now and remove it from the Mods page in Toys first.' + #13#10#13#10 + 'Your settings, replays, and skins will also be kept.' + #13#10#13#10 + 'Continue uninstalling Toys?', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES;
end;
