#ifndef AppVersion
#define AppVersion "2.0.1"
#endif
#ifndef PublishDir
#define PublishDir "..\artifacts\publish"
#endif
#ifndef InstallerDir
#define InstallerDir "..\artifacts\installer"
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
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\KeyLearner"; Filename: "{app}\KeyLearner.exe"; WorkingDir: "{app}"; AppUserModelID: "KeyLearner.Desktop"
Name: "{group}\KeyLearner Parent Studio"; Filename: "{app}\KeyLearner.exe"; Parameters: "--preview --studio"; WorkingDir: "{app}"; AppUserModelID: "KeyLearner.Desktop"
Name: "{autodesktop}\KeyLearner"; Filename: "{app}\KeyLearner.exe"; WorkingDir: "{app}"; Tasks: desktopicon; AppUserModelID: "KeyLearner.Desktop"

[Run]
Filename: "{app}\KeyLearner.exe"; Parameters: "--preview --studio"; Description: "Open the parent studio"; Flags: nowait postinstall skipifsilent unchecked

; Profiles live separately under LocalAppData\KeyLearner. Neither upgrades nor
; uninstall remove dictionaries, recordings, settings, or learned preferences.
