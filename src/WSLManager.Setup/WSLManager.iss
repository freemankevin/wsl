; WSL Manager Setup Script for Inno Setup 6
; Builds a multilingual Windows installer with MIT license, desktop shortcut option,
; customizable install path, and completion page.

#define MyAppName "WSL Manager"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "WSLManager"
#define MyAppExeName "WSLManager.exe"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=LICENSE.txt
OutputDir=..\..\artifacts
OutputBaseFilename=WSLManager-{#MyAppVersion}-Setup
SetupIconFile=..\..\public\favicon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ShowLanguageDialog=yes

[Languages]
Name: "chinesesimplified"; MessagesFile: "InnoLanguages\ChineseSimplified.isl"; LicenseFile: "LICENSE.txt"
Name: "english"; MessagesFile: "compiler:Default.isl"; LicenseFile: "LICENSE.txt"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
Source: "..\..\artifacts\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\artifacts\publish\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\artifacts\publish\Images\*"; DestDir: "{app}\Images"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\..\public\favicon.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\favicon.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; IconFilename: "{app}\favicon.ico"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[CustomMessages]
english.LaunchProgram=Launch %1
chinesesimplified.LaunchProgram=启动 %1
