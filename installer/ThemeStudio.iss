#ifndef AppVersion
#define AppVersion "0.1.0"
#endif

#define AppName "ChatGPT Desktop Theme Studio"
#define AppPublisher "Theme Studio contributors"
#define AppExeName "ThemeStudio.exe"

[Setup]
AppId={{1203DC85-8C31-4B9C-B555-3B082A2A8A6C}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\ThemeStudio
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
OutputBaseFilename=ThemeStudio-Setup
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
Uninstallable=yes

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: postinstall nowait skipifsilent

[UninstallDelete]
; Themes and imported media live under the user's LocalAppData and are intentionally preserved.
