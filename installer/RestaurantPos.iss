; Restaurant POS. Compile with the pinned Inno Setup 6.4.3 compiler.
#ifndef BundleFramework
#define BundleFramework 0
#endif
#ifndef InstallerSmoke
#define InstallerSmoke 0
#endif
[Setup]
#if InstallerSmoke
AppId={{59607345-81E7-41D0-A32D-F51A82E50DCC}
#else
AppId={{D7731694-AAB5-4ECD-A1C7-C6C8E068149C}
#endif
AppName=Restaurant POS
AppVersion=1.1.2
DefaultDirName={localappdata}\Programs\RestaurantPos
DefaultGroupName=Restaurant POS
PrivilegesRequired=lowest
MinVersion=6.1sp1
OutputDir=..\artifacts
#if InstallerSmoke
OutputBaseFilename=Setup-Smoke
#elif BundleFramework
OutputBaseFilename=Setup
#else
OutputBaseFilename=Setup-Small
#endif
SetupIconFile=..\src\RestaurantPos\Assets\logo.ico
UninstallDisplayIcon={app}\RestaurantPos.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Files]
#if BundleFramework
Source: "..\.tools\ndp48-x86-x64-allos-enu.exe"; DestDir: "{tmp}"; Flags: dontcopy
#endif
Source: "..\src\RestaurantPos\bin\Release\net48\RestaurantPos.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\RestaurantPos\bin\Release\net48\RestaurantPos.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\RestaurantPos\bin\Release\net48\System.Data.SQLite.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\RestaurantPos\bin\Release\net48\x86\SQLite.Interop.dll"; DestDir: "{app}\x86"; Flags: ignoreversion
Source: "..\src\RestaurantPos\bin\Release\net48\x64\SQLite.Interop.dll"; DestDir: "{app}\x64"; Flags: ignoreversion
Source: "..\USER-GUIDE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
#if InstallerSmoke
#else
Name: "{group}\Restaurant POS"; Filename: "{app}\RestaurantPos.exe"
Name: "{autodesktop}\Restaurant POS"; Filename: "{app}\RestaurantPos.exe"; Tasks: desktopicon
#endif

[Tasks]
Name: desktopicon; Description: "Create a Desktop shortcut"

[Run]
#if InstallerSmoke
Filename: "{app}\RestaurantPos.exe"; Parameters: "--data-root ""{app}\smoke-data"" --initialize"; Flags: runhidden waituntilterminated; Check: CanInitialize
#else
Filename: "{app}\RestaurantPos.exe"; Parameters: "--initialize"; Flags: runhidden waituntilterminated; Check: CanInitialize
Filename: "{app}\RestaurantPos.exe"; Parameters: "--configure-accounts"; Flags: waituntilterminated; Check: CanConfigureAccounts
#endif

[Code]
function HasFramework(): Boolean;
var Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040);
end;

var FrameworkNeedsRestart: Boolean;

function CanInitialize(): Boolean;
begin
  Result := HasFramework() and not FrameworkNeedsRestart;
end;

function CanConfigureAccounts(): Boolean;
begin
  Result := CanInitialize() and not WizardSilent();
end;

function InitializeSetup(): Boolean;
begin
#if BundleFramework
  Result := True;
#else
  Result := HasFramework();
  if not Result then
    MsgBox('Microsoft .NET Framework 4.8 is required. Use the full Setup.exe package which includes the offline prerequisite, or install Framework 4.8 first. No application data has been changed.', mbError, MB_OK);
#endif
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var ExitCode: Integer;
begin
  Result := '';
#if BundleFramework
  if not HasFramework() then begin
    ExtractTemporaryFile('ndp48-x86-x64-allos-enu.exe');
    if not ShellExec('runas', ExpandConstant('{tmp}\ndp48-x86-x64-allos-enu.exe'), '/passive /norestart', '', SW_SHOW, ewWaitUntilTerminated, ExitCode) then begin
      Result := 'Microsoft .NET Framework 4.8 installation was cancelled or could not start.';
      Exit;
    end;
    if (ExitCode <> 0) and (ExitCode <> 3010) and (ExitCode <> 1641) then begin
      Result := 'Microsoft .NET Framework 4.8 installation failed. Exit code: ' + IntToStr(ExitCode);
      Exit;
    end;
    FrameworkNeedsRestart := (ExitCode = 3010) or (ExitCode = 1641);
    NeedsRestart := FrameworkNeedsRestart;
    if not HasFramework() and not NeedsRestart then Result := 'Framework 4.8 could not be detected after installation.';
  end;
#endif
end;

function NeedRestart(): Boolean;
begin
  Result := FrameworkNeedsRestart;
end;

// Writable data is outside {app}; uninstall deliberately preserves it.


