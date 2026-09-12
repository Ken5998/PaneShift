; Version and paths are supplied by scripts/Package-Release.ps1.
#ifndef AppVersion
  #error AppVersion must be supplied
#endif
#ifndef PublishDir
  #error PublishDir must be supplied
#endif
#ifndef ArtifactDir
  #error ArtifactDir must be supplied
#endif
#define ProjectRoot SourcePath + ".."
#define AppExe "PaneShift.App.exe"

[Setup]
AppId={{84308E17-6F94-43D7-9367-38DD674BB41E}
AppName=PaneShift
AppVersion={#AppVersion}
AppPublisher=Kenan Kasumović
AppPublisherURL=https://github.com/Ken5998/PaneShift
AppSupportURL=https://github.com/Ken5998/PaneShift/issues
AppUpdatesURL=https://github.com/Ken5998/PaneShift/releases
DefaultDirName={localappdata}\Programs\PaneShift
DefaultGroupName=PaneShift
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
AppMutex=Local\PaneShift
CloseApplications=no
RestartApplications=no
SetupIconFile={#ProjectRoot}\assets\paneshift.ico
UninstallDisplayIcon={app}\{#AppExe}
LicenseFile={#ProjectRoot}\LICENSE
OutputDir={#ArtifactDir}
OutputBaseFilename=PaneShift-{#AppVersion}-Setup-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
DisableWelcomePage=no
Uninstallable=yes
VersionInfoVersion={#FileVersion}
VersionInfoProductVersion={#FileVersion}
VersionInfoProductTextVersion={#AppVersion}
VersionInfoDescription=PaneShift Setup
VersionInfoCopyright=Copyright (c) 2026 Kenan Kasumović

[Tasks]
Name: "startmenu"; Description: "Create a Start Menu shortcut"; GroupDescription: "Shortcuts:"
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\PaneShift"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: startmenu
Name: "{userdesktop}\PaneShift"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch PaneShift"; Flags: nowait postinstall skipifsilent runasoriginaluser

; Installation never enables startup. Remove only this installed copy's opt-in on uninstall.
; Inno's uninstall log removes installed files/shortcuts only; settings live elsewhere.

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  StartupCommand: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'PaneShift', StartupCommand) then
      if CompareText(StartupCommand, '"' + ExpandConstant('{app}\{#AppExe}') + '"') = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'PaneShift');
end;
