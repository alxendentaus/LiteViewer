; 日志查看器安装脚本
#define MyAppName "LiteViewer 日志查看器"
#define MyAppShort "LiteViewer"
#define MyAppVersion "1.0"
#define MyAppPublisher "LiteViewer"
#define MyAppExeName "NativeLogViewer.exe"
#define MyAppIcon "C:\Users\Administrator\WorkBuddy\2026-11-26-14-28-03\NativeLogViewer\Resources\app.ico"
#define MyExeSource "C:\Users\Administrator\WorkBuddy\2026-11-26-14-28-03\NativeLogViewer\publish-standalone\NativeLogViewer.exe"

[Setup]
AppId={{8E4B1F2A-6C3D-4A5E-9B17-2D8F0A6C4E91}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\LiteViewer
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
SetupIconFile={#MyAppIcon}
OutputDir=C:\Users\Administrator\WorkBuddy\2026-11-26-14-28-03\NativeLogViewer\installer
OutputBaseFilename=LiteViewer-Setup-v1.0
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
PrivilegesRequired=admin
UsedUserAreasWarning=no
LicenseFile=

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create desktop shortcut"; GroupDescription: "Additional shortcuts:"
Name: "contextmenu"; Description: "Add 'Open with LiteViewer' to file right-click menu"; GroupDescription: "Shell integration:"

[Files]
Source: "{#MyExeSource}"; DestDir: "{app}"; DestName: {#MyAppExeName}; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppShort}"; Filename: "{uninstallexe}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; 右键菜单（HKCU，卸载时自动清理）
Root: HKCU; Subkey: "Software\Classes\*\shell\LiteViewer"; ValueType: string; ValueName: ""; ValueData: "用日志查看器打开"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\*\shell\LiteViewer"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName}"; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\*\shell\LiteViewer\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: contextmenu; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppShort} now"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: files; Name: "{localappdata}\LiteViewer\settings.json"
