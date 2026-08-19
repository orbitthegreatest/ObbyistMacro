; ObbyistMacro installer
; Compile: "C:\Users\tutot\AppData\Local\Temp\opencode\innosetup\ISCC.exe" ObbyistMacro.iss

#define MyAppName "ObbyistMacro"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "ObbyistMacro"
#define MyAppExeName "ObbyistMacro.exe"

[Setup]
AppId={{8E0B5C1A-3D2F-4A6B-9C8E-5F1A2B3C4D5E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={commonpf32}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=ObbyistMacro-Setup
SetupIconFile=..\ObbyiestMacro.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\src\publish\ObbyistMacro.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\ObbyiestMacro.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\ObbyiestMacro.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\ObbyiestMacro.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent