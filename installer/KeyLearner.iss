#ifndef AppVersion
#define AppVersion "2.0.1"
#endif
#ifndef PublishDir
#define PublishDir "..\artifacts\publish"
#endif
#ifndef InstallerDir
#define InstallerDir "..\artifacts\installer"
#endif

#ifdef UnityPort
#define PlayArguments "-screen-fullscreen 1"
#define StudioArguments "-screen-fullscreen 0 --studio"
#define LaunchWindowFlags "runmaximized"
#else
#define PlayArguments ""
#define StudioArguments "--preview --studio"
#define LaunchWindowFlags ""
#endif

[Setup]
; Permanent identity: never change this between releases.
AppId={{D5C654D0-1B14-4479-B771-20BD264731A8}
AppName=KeyLearner
AppVersion={#AppVersion}
AppPublisher=KeyLearner
AppPublisherURL=https://github.com/faradaysage/Key-Learner
DefaultDirName={localappdata}\Programs\KeyLearner
DefaultGroupName=KeyLearner
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
UsePreviousAppDir=yes
UsePreviousGroup=yes
DisableProgramGroupPage=yes
DisableDirPage=auto
UninstallDisplayName=KeyLearner
UninstallDisplayIcon={app}\KeyLearner.exe
SetupIconFile=..\Content\Branding\KeyLearner.ico
OutputDir={#InstallerDir}
OutputBaseFilename=KeyLearner-{#AppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#AppVersion}

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
#ifdef UnityPort
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*_BackUpThisFolder_ButDontShipItWithYourGame,*_BackUpThisFolder_ButDontShipItWithYourGame\*,*_BurstDebugInformation_DoNotShip,*_BurstDebugInformation_DoNotShip\*,*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
#else
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
#endif

[Icons]
Name: "{group}\KeyLearner"; Filename: "{app}\KeyLearner.exe"; Parameters: "{#PlayArguments}"; WorkingDir: "{app}"; AppUserModelID: "KeyLearner.Desktop"; Flags: {#LaunchWindowFlags}
Name: "{group}\KeyLearner Parent Studio"; Filename: "{app}\KeyLearner.exe"; Parameters: "{#StudioArguments} --data ""{localappdata}\KeyLearner"""; WorkingDir: "{app}"; AppUserModelID: "KeyLearner.Desktop"; Flags: {#LaunchWindowFlags}
Name: "{autodesktop}\KeyLearner"; Filename: "{app}\KeyLearner.exe"; Parameters: "{#PlayArguments}"; WorkingDir: "{app}"; Tasks: desktopicon; AppUserModelID: "KeyLearner.Desktop"; Flags: {#LaunchWindowFlags}

[Run]
Filename: "{app}\KeyLearner.exe"; Parameters: "{#StudioArguments} --data ""{localappdata}\KeyLearner"""; Description: "Open the parent studio"; Flags: nowait postinstall skipifsilent unchecked {#LaunchWindowFlags}

; Profiles live separately under LocalAppData\KeyLearner. Neither upgrades nor
; uninstall remove dictionaries, recordings, settings, or learned preferences.


#ifdef UnityPort
#include "LegacyMonoGameFiles.iss"
#endif
