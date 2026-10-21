; Restaurant POS. Compile with the pinned Inno Setup 6.4.3 compiler.
#ifndef BundleFramework
#define BundleFramework 0
#endif
#ifndef InstallerSmoke
#define InstallerSmoke 0
#endif
[Setup]
#if InstallerSmoke
AppId={{AAC0A0E3-76F5-4D61-A851-384E2F3606CF}
#else
AppId={{D12921E1-B739-4E30-93F3-04DAB36650B4}
#endif
AppName=Push
AppVersion=1.2.3
DefaultDirName={localappdata}\Programs\PushPOS
DefaultGroupName=Push
PrivilegesRequired=lowest
MinVersion=6.1sp1
OutputDir=..\artifacts
#if InstallerSmoke
OutputBaseFilename=Setup-Smoke
#elif BundleFramework
OutputBaseFilename=PushPOS-Setup
#else
OutputBaseFilename=PushPOS-Setup-Small
#endif
UninstallDisplayIcon={app}\{code:GetExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Files]
#if BundleFramework
Source: "..\.tools\ndp48-x86-x64-allos-enu.exe"; DestDir: "{tmp}"; Flags: dontcopy
#endif
Source: "..\src\RestaurantPos\bin\Release\net48\RestaurantPos.exe"; DestDir: "{app}"; DestName: "{code:GetExeName}"; Flags: ignoreversion
Source: "..\src\RestaurantPos\bin\Release\net48\RestaurantPos.exe.config"; DestDir: "{app}"; DestName: "{code:GetExeName}.config"; Flags: ignoreversion
Source: "..\src\RestaurantPos\bin\Release\net48\System.Data.SQLite.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\RestaurantPos\bin\Release\net48\x86\SQLite.Interop.dll"; DestDir: "{app}\x86"; Flags: ignoreversion
Source: "..\src\RestaurantPos\bin\Release\net48\x64\SQLite.Interop.dll"; DestDir: "{app}\x64"; Flags: ignoreversion
Source: "..\USER-GUIDE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\tools\PushPOS.BrandingTool\bin\Release\net48\PushPOS.BrandingTool.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion; AfterInstall: WriteBranding

[InstallDelete]
Type: files; Name: "{app}\{code:GetOldExe}"; Check: RemoveOldExe
Type: files; Name: "{app}\{code:GetOldExe}.config"; Check: RemoveOldExe

[Icons]
Name: "{group}\{code:GetBusinessName}"; Filename: "{app}\{code:GetExeName}"
Name: "{autodesktop}\{code:GetBusinessName}"; Filename: "{app}\{code:GetExeName}"; Tasks: desktopicon

[Tasks]
Name: desktopicon; Description: "Create a Desktop shortcut"

[Run]
#if InstallerSmoke
Filename: "{app}\{code:GetExeName}"; Parameters: "--data-root ""{app}\smoke-data"" --initialize"; Flags: runhidden waituntilterminated; Check: CanInitialize
#else
Filename: "{app}\{code:GetExeName}"; Parameters: "--initialize"; Flags: runhidden waituntilterminated; Check: CanInitialize
Filename: "{app}\{code:GetExeName}"; Parameters: "--configure-accounts"; Flags: waituntilterminated; Check: CanConfigureAccounts
#endif

[Code]
var BusinessPage: TInputQueryWizardPage;
    LogoPage: TInputFileWizardPage;

function ValidBusinessName(Value: String): Boolean;
var I: Integer; Upper, BadChars, BadChar: String;
begin
  Value := Trim(Value);
  Result := (Length(Value) > 0) and (Length(Value) <= 60);
  if not Result then Exit;
  BadChars := '\/:*?"<>|{}';
  for I := 1 to Length(BadChars) do begin
    BadChar := BadChars[I];
    if Pos(BadChar, Value) > 0 then Result := False;
  end;
  for I := 1 to 31 do begin BadChar := Chr(I);if Pos(BadChar, Value) > 0 then Result := False;end;
  if Value[Length(Value)] = '.' then Result := False;
  Upper := Uppercase(Value);
  if Copy(Upper,1,5)='UNINS' then Result := False;
  if Pos('.', Upper) > 0 then Upper := Copy(Upper, 1, Pos('.', Upper)-1);
  if (Upper='CON') or (Upper='PRN') or (Upper='AUX') or (Upper='NUL') then Result := False;
  if Length(Upper)=4 then
    if ((Copy(Upper,1,3)='COM') or (Copy(Upper,1,3)='LPT')) and (Upper[4]>='0') and (Upper[4]<='9') then Result := False;
end;

function GetBusinessName(Param: String): String;
begin
  Result := 'Push';
  if BusinessPage <> nil then Result := Trim(BusinessPage.Values[0]);
  if not ValidBusinessName(Result) then Result := 'Push';
end;

function GetExeName(Param: String): String;
begin
  Result := GetBusinessName('') + '.exe';
end;

function GetOldExe(Param: String): String;
var OldName: String;
begin
  OldName:=GetPreviousData('BusinessName','');
  if ValidBusinessName(OldName) then Result:=OldName+'.exe' else Result:='RestaurantPos.exe';
end;

function RemoveOldExe(): Boolean;
begin
  Result:=CompareText(GetOldExe(''),GetExeName(''))<>0;
end;

procedure InitializeWizard();
begin
  BusinessPage := CreateInputQueryPage(wpSelectDir, 'Restaurant details', 'اسم المطعم وبياناته', 'سيتم تسمية ملف التشغيل والاختصار باسم المطعم. يمكنك تعديل بيانات الإيصال لاحقًا من الإعدادات.');
  BusinessPage.Add('اسم المطعم / اسم ملف التشغيل:', False);
  BusinessPage.Add('التليفونات:', False);
  BusinessPage.Add('العنوان:', False);
  BusinessPage.Add('ختام الإيصال:', False);
  BusinessPage.Values[0] := ExpandConstant('{param:BUSINESSNAME|}');
  Log('Requested business name: '+BusinessPage.Values[0]);
  if ValidBusinessName(BusinessPage.Values[0]) then Log('Business filename validation: valid') else Log('Business filename validation: invalid');
  if not ValidBusinessName(BusinessPage.Values[0]) then BusinessPage.Values[0] := GetPreviousData('BusinessName', 'Push');
  BusinessPage.Values[1] := ExpandConstant('{param:BUSINESSPHONE|}');
  BusinessPage.Values[2] := ExpandConstant('{param:BUSINESSADDRESS|}');
  BusinessPage.Values[3] := ExpandConstant('{param:BUSINESSFOOTER|شكرًا لزيارتكم}');
  LogoPage := CreateInputFilePage(BusinessPage.ID, 'Restaurant image', 'صورة المطعم', 'اختياري: اختر صورة تظهر بجوار شاشة الكاشير وعلى الإيصال.');
  LogoPage.Add('صورة المطعم:', 'Images|*.png;*.jpg;*.jpeg;*.bmp;*.ico', '.png');
  LogoPage.Values[0] := ExpandConstant('{param:BUSINESSLOGO|}');
  Log('Resolved business name: '+GetBusinessName(''));
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var I: Integer;
begin
  Result := True;
  if CurPageID=BusinessPage.ID then begin
    Result := ValidBusinessName(BusinessPage.Values[0]);
    for I:=1 to 3 do if Length(BusinessPage.Values[I])>180 then Result := False;
    if not Result then MsgBox('أدخل اسم مطعم صالحًا للملفات، بدون رموز خاصة، وبحد أقصى 60 حرفًا. باقي الحقول بحد أقصى 180 حرفًا.', mbError, MB_OK);
  end;
  if (CurPageID=LogoPage.ID) and (LogoPage.Values[0]<>'') and not FileExists(LogoPage.Values[0]) then begin Result:=False;MsgBox('ملف الصورة غير موجود.',mbError,MB_OK);end;
end;

procedure RegisterPreviousData(PreviousDataKey: Integer);
begin
  SetPreviousData(PreviousDataKey, 'BusinessName', GetBusinessName(''));
end;

procedure WriteBranding();
var Lines: TArrayOfString; IconExitCode: Integer;
begin
  begin
    SetArrayLength(Lines,5);
    Lines[0]:='[Business]';Lines[1]:='Name='+GetBusinessName('');Lines[2]:='Phone='+BusinessPage.Values[1];Lines[3]:='Address='+BusinessPage.Values[2];Lines[4]:='Footer='+BusinessPage.Values[3];
    if not SaveStringsToUTF8File(ExpandConstant('{app}\branding.ini'),Lines,False) then RaiseException('Could not save restaurant settings');
    if (LogoPage.Values[0]<>'') and not CopyFile(LogoPage.Values[0],ExpandConstant('{app}\setup-logo.image'),False) then RaiseException('Could not copy restaurant image');
    if FileExists(ExpandConstant('{app}\setup-logo.image')) then begin
      if not Exec(ExpandConstant('{app}\PushPOS.BrandingTool.exe'), '"'+ExpandConstant('{app}\setup-logo.image')+'" "'+ExpandConstant('{app}\{code:GetExeName}')+'"', ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, IconExitCode) then RaiseException('Could not start executable icon customization');
      if IconExitCode<>0 then RaiseException('Could not apply restaurant image to executable icon. Please select a valid PNG, JPG, BMP or ICO image.');
    end;
  end;
end;

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


