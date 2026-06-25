#define AppName      "EWRC Scraper"
; AppVersion can be overridden from the command line: ISCC /DAppVersion=1.3.2 ...
#ifndef AppVersion
  #define AppVersion "1.4.1"
#endif
#define AppPublisher "Rally Club Holland"
#define AppURL       "https://www.rallyclubholland.nl"
#define AppExeName   "EwrcScraper.exe"
#define SourceDir    "..\CSharpScraper\publish\standalone"

[Setup]
AppId={{B7A4C2E1-9F3D-4A8B-BC6E-2D5F1A3E7C90}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL=https://github.com/ITMarco/EwrcScraper/releases
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=.\Output
OutputBaseFilename=EwrcScraper-v{#AppVersion}-Setup
SetupIconFile=..\CSharpScraper\Resources\ewrc-rch.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardSmallImageFile=..\CSharpScraper\Resources\Images\ewrc-logo.png
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
; Auto-close a running copy (e.g. during an in-app update) so locked files can be replaced
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} — RCH inschrijvingsvergelijker

[Languages]
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"

[Tasks]
Name: "desktopicon"; Description: "Snelkoppeling op het bureaublad"; GroupDescription: "Extra taken:"

[Files]
; Main executable (self-contained — includes .NET runtime)
Source: "{#SourceDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

; Native WPF DLLs required alongside the exe
Source: "{#SourceDir}\D3DCompiler_47_cor3.dll";     DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\PenImc_cor3.dll";             DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\PresentationNative_cor3.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\vcruntime140_cor3.dll";       DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\wpfgfx_cor3.dll";             DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\version.json";                DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}";        Filename: "{app}\{#AppExeName}"
Name: "{group}\Verwijder {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";  Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Start {#AppName}"; Flags: nowait postinstall skipifsilent
