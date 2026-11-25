; PCOpti installer (Inno Setup 6). Build with installer\build-installer.ps1, or:
;   ISCC.exe /DAppVersion=1.0.0 installer\PCOpti.iss
; Expects the published single-file exe at publish\PCOpti.exe (repo root).

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppName "PCOpti"
#define AppExe "PCOpti.exe"
#define AppUrl "https://github.com/isxxc21/PCOptim"

[Setup]
; AppId identifies the install for upgrades and uninstall. Never change it.
AppId={{6F3C2A1E-9B4D-4E8A-A7C2-5D1F0B3E8C47}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=PCOpti contributors
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=Output
OutputBaseFilename=PCOpti-Setup-v{#AppVersion}
SetupIconFile=..\src\PCOpti\Assets\pcopti.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; PCOpti itself needs admin rights, so install machine-wide into Program Files.
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Windows 10 version 2004 or newer.
MinVersion=10.0.19041
; Close a running PCOpti before upgrading or uninstalling.
CloseApplications=yes

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\publish\PCOpti.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; Comment: "Honest PC optimiser for gamers"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Code]
// Be honest at uninstall time: removing the app doesn't undo its changes.
// Backups in %LocalAppData%\PCOpti are deliberately kept so a reinstall can still revert.
function InitializeUninstall(): Boolean;
begin
  Result := True;
  if not UninstallSilent then
    Result := MsgBox(
      'Uninstalling PCOpti does NOT undo the changes it made to Windows.' + #13#10#13#10 +
      'To undo them first, click No, open PCOpti and use Backup & Restore > "Revert all PCOpti changes".' + #13#10#13#10 +
      'Your backups are kept in %LocalAppData%\PCOpti, so you can also reinstall later and revert then.' + #13#10#13#10 +
      'Uninstall PCOpti now?',
      mbConfirmation, MB_YESNO) = IDYES;
end;
