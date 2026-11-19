; Inno Setup 6.7 script. Built by build-release.ps1:
;   ISCC /DAppVersion=x.y.z /DStageDir=<staged folder> /DOutputDir=<out> OpenGimbalCam.iss
#ifndef AppVersion
  #error Define AppVersion (/DAppVersion=x.y.z)
#endif
#ifndef StageDir
  #error Define StageDir
#endif
#ifndef OutputDir
  #define OutputDir "."
#endif
[Setup]
; Generated once. Never change it: it identifies the app for upgrades and uninstall.
AppId={{F239E5AD-F4F2-4291-BC1E-E710648A0D62}
AppName=OpenGimbalCam
AppVersion={#AppVersion}
AppVerName=OpenGimbalCam {#AppVersion}
AppPublisher=OpenGimbalCam
AppPublisherURL=https://github.com/alsimoes/opengimbalcam
AppSupportURL=https://github.com/alsimoes/opengimbalcam/issues
AppUpdatesURL=https://github.com/alsimoes/opengimbalcam/releases
VersionInfoVersion={#AppVersion}.0
DefaultDirName={autopf}\OpenGimbalCam
DefaultGroupName=OpenGimbalCam
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
LicenseFile={#StageDir}\LICENSE
SetupIconFile=..\..\src\OpenGimbalCam.Studio\Assets\app.ico
UninstallDisplayIcon={app}\opengimbalcam-studio.exe
UninstallDisplayName=OpenGimbalCam {#AppVersion}
OutputDir={#OutputDir}
OutputBaseFilename=OpenGimbalCam-{#AppVersion}-win-x64-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes

[Languages]
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#StageDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\OpenGimbalCam Studio"; Filename: "{app}\opengimbalcam-studio.exe"
Name: "{autodesktop}\OpenGimbalCam Studio"; Filename: "{app}\opengimbalcam-studio.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\opengimbalcam-studio.exe"; Description: "{cm:LaunchProgram,OpenGimbalCam Studio}"; Flags: nowait postinstall skipifsilent
