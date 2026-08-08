; NewFPS Converter installer
; Requires Inno Setup 6.7.3 or newer stable 6.x.
; Build the app first with:
;   dotnet publish FramerateConverter\NewFPSConverter.csproj -c Release

#define MyAppName "NewFPS Converter"
#define MyAppPublisher "NewFPS Converter"
#define MyAppExeName "NewFPSConverter.exe"
#define MyAppRegistryKey "Software\NewFPSConverter"
#define MyAppBuildDir "FramerateConverter\bin\Release\net10.0-windows\publish"
#define MyAppExePath MyAppBuildDir + "\" + MyAppExeName
#define MyAppVersion GetVersionNumbersString(MyAppExePath)

[Setup]
AppId={{97ED9B22-2716-407B-A64C-05D9560116D1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=FramerateConverter\Templates\GPLv2.txt
OutputDir=.
OutputBaseFilename=setupNewFPSConverter-{#MyAppVersion}
SetupIconFile=FramerateConverter\Images\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName},0
UninstallDisplayName={#MyAppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MyAppBuildDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Registry]
Root: HKCU; Subkey: "{#MyAppRegistryKey}"; ValueType: string; ValueName: "InstallationPath"; ValueData: "{app}\{#MyAppExeName}"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "{#MyAppRegistryKey}"; ValueType: string; ValueName: "InstalledVersion"; ValueData: "{#MyAppVersion}"; Flags: uninsdeletevalue

[InstallDelete]
Type: filesandordirs; Name: "{app}\plugins"
Type: filesandordirs; Name: "{app}\Licenses"
Type: files; Name: "{app}\FreeVideoFPSConverter.exe"
Type: files; Name: "{app}\FramerateConverter.exe"
Type: files; Name: "{app}\Xceed.Wpf.Toolkit.dll"
Type: files; Name: "{app}\DotNetZip.dll"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
