#ifndef AppVersion
  #define AppVersion "0.1.0-prototype"
#endif
#ifndef StageDir
  #define StageDir "out\app"
#endif
; A live-test build passes its own name, so it can never pass for a release.
#ifndef SetupBase
  #define SetupBase "SoloStone-Setup-" + AppVersion
#endif
[Setup]
AppId=SoloStone
AppName=SoloStone
AppVersion={#AppVersion}
UninstallDisplayName=SoloStone
DefaultDirName={localappdata}\SoloStone\app
PrivilegesRequired=lowest
; Per-user install. Running elevated is allowed (owner 2026-11-08: players with UAC turned off are always elevated); only the System32 cmd/netsh plan asks for UAC.
DisableDirPage=yes
DefaultGroupName=SoloStone
DisableProgramGroupPage=yes
; Inno shows only its progress bar. Everything the player decides happens in SoloStone's own setup window.
DisableWelcomePage=yes
DisableReadyPage=yes
SetupIconFile=assets\installer.ico
DisableReadyMemo=yes
DisableFinishedPage=yes
OutputDir=out
OutputBaseFilename={#SetupBase}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
UninstallDisplayIcon={app}\SoloStone.exe
CloseApplications=no
RestartApplications=no
[Files]
Source: "{#StageDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#StageDir}\SoloStone.exe"; Flags: dontcopy
[Icons]
; No arguments: the launcher finds its own folder, opens the launcher window, or the setup window when no copy exists yet.
Name: "{userprograms}\SoloStone"; Filename: "{app}\SoloStone.exe"
Name: "{userdesktop}\SoloStone"; Filename: "{app}\SoloStone.exe"
[UninstallDelete]
; An update brings files that Inno never listed. This runs only after InitializeUninstall has verified ownership of {app} and the launcher has removed the copy and firewall rules.
Type: filesandordirs; Name: "{app}"
[Code]
var SetupResult: Integer;
function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := PageID = wpReady;
end;
function GetFileAttributes(Path: String): LongWord;
  external 'GetFileAttributesW@kernel32.dll stdcall';
function PlainPath(Path: String): Boolean;
var Parent: String; Attr: LongWord;
begin
  Result := False;
  while Path <> '' do begin
    Attr := GetFileAttributes(Path);
    if (Attr <> $FFFFFFFF) and ((Attr and $400) <> 0) then exit;
    Parent := ExtractFileDir(Path);
    if Parent = Path then break;
    Path := Parent;
  end;
  Result := True;
end;
function PlainTree(Path: String): Boolean;
var Entry: TFindRec;
begin
  Result := PlainPath(Path);
  if not Result then exit;
  if FindFirst(Path + '\*', Entry) then begin
    try
      repeat
        if (Entry.Name <> '.') and (Entry.Name <> '..') then begin
          if (Entry.Attributes and $400) <> 0 then begin Result := False; exit; end;
          if ((Entry.Attributes and $10) <> 0) and not PlainTree(Path + '\' + Entry.Name) then begin Result := False; exit; end;
        end;
      until not FindNext(Entry);
    finally
      FindClose(Entry);
    end;
  end;
end;
function GetRoot(Param: String): String;
begin
  Result := ExtractFileDir(ExpandConstant('{app}'));
end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
var Code: Integer;
begin
  Result := '';
  ExtractTemporaryFile('SoloStone.exe');
  Code := 1;
  if not ExecAsOriginalUser(ExpandConstant('{tmp}\SoloStone.exe'),
      'preflight --quiet --root "' + GetRoot('') + '" --app "' + ExpandConstant('{app}') + '"', '', SW_HIDE, ewWaitUntilTerminated, Code)
      or (Code <> 0) then
    if Code = 3 then
      Result := 'SoloStone is finishing an update. Wait for its window to close, then run setup again.'
    else if Code = 4 then
      Result := 'This folder contains another or incomplete installation. Open SoloStone from the Start menu, or see the installer log before trying again.'
    else if Code = 5 then
      Result := 'Couldn''t read the install folder. Close programs using it and try again.'
    else
      Result := 'This installation folder is unsafe. See the installer log and resolve the reported problem before trying again.';
  if (Result = '') and FileExists(ExpandConstant('{app}\SoloStone.exe')) then begin
    try
      if GetSHA256OfFile(ExpandConstant('{app}\SoloStone.exe')) <> GetSHA256OfFile(ExpandConstant('{tmp}\SoloStone.exe')) then begin
        Log('Existing app executable differs from this installer; refusing overwrite.');
        Result := 'This folder contains a different SoloStone installation. Open it from the Start menu or uninstall it before trying again.';
      end;
    except
      Log(GetExceptionMessage);
      Result := 'Couldn''t read the install folder. Close programs using it and try again.';
    end;
  end;
end;
procedure CurStepChanged(CurStep: TSetupStep);
var Params: String; Source: String;
begin
  if (CurStep = ssPostInstall) and (ExpandConstant('{param:SKIPWIZARD|0}') <> '1') then begin
    Source := ExpandConstant('{param:SOURCE|}');
    if Source <> '' then
      { A script names the Hearthstone folder: no window, same checks. }
      Params := 'setup --quiet --root "' + GetRoot('') + '" --source "' + Source + '"'
    else
      Params := 'wizard --root "' + GetRoot('') + '"';
    if (Source = '') and not WizardSilent then WizardForm.Hide;
    try
      if not ExecAsOriginalUser(ExpandConstant('{app}\SoloStone.exe'), Params, '', SW_SHOWNORMAL,
          ewWaitUntilTerminated, SetupResult) then SetupResult := 1;
    finally
      if (Source = '') and not WizardSilent then WizardForm.Show;
    end;
    { Closing the wizard before step 5 is not an error: it exits 0 and the shortcut opens it again. Only a setup that failed gets a note. }
    if SetupResult <> 0 then
      MsgBox('SoloStone is installed, but setting up the offline copy did not finish. Open SoloStone from the Start menu to try again. Your own Hearthstone and your saves were not changed.', mbInformation, MB_OK);
  end;
end;
function GetCustomSetupExitCode: Integer;
begin Result := SetupResult; end;
{ A kept launcher must not run from a folder uninstall is about to delete, so it and its libraries go to a temp folder first. }
function CopyLauncher(Folder: String): String;
var Entry: TFindRec; Target: String;
begin
  Target := ExpandConstant('{tmp}\fallback');
  ForceDirectories(Target);
  if FindFirst(Folder + '\*', Entry) then begin
    try
      repeat
        if (Entry.Attributes and $10) = 0 then FileCopy(Folder + '\' + Entry.Name, Target + '\' + Entry.Name, False);
      until not FindNext(Entry);
    finally
      FindClose(Entry);
    end;
  end;
  Result := Target + '\SoloStone.exe';
end;
{ The app's launcher, or after a power cut took it mid-update, a copy of the kept one. }
function FindLauncher(App, Root: String): String;
var Entry: TFindRec; Base: String; Pass: Integer;
begin
  Result := App + '\SoloStone.exe';
  if FileExists(Result) then exit;
  for Pass := 0 to 1 do begin
    if Pass = 0 then Base := Root + '\releases' else Base := Root + '\updates';
    if FindFirst(Base + '\*', Entry) then begin
      try
        repeat
          if (Entry.Name <> '.') and (Entry.Name <> '..') and FileExists(Base + '\' + Entry.Name + '\app\SoloStone.exe') then begin
            Result := CopyLauncher(Base + '\' + Entry.Name + '\app');
            exit;
          end;
        until not FindNext(Entry);
      finally
        FindClose(Entry);
      end;
    end;
  end;
end;
function InitializeUninstall: Boolean;
var Code: Integer; App: String; Root: String; Params: String; Launcher: String;
begin
  Result := False;
  App := ExpandConstant('{app}');
  Root := ExtractFileDir(App);
  { The launcher window warns before it starts this silently. From Windows Settings this is the only warning. }
  if not UninstallSilent then
    if MsgBox('Remove SoloStone and its offline copy of Hearthstone?' + #13#10#13#10 + 'Your own Hearthstone and your saves are kept.' + #13#10 + 'Anything you added to the copy folder is deleted too.', mbConfirmation, MB_YESNO) <> IDYES then exit;
  { Do not execute a helper through a redirected app path. The helper then checks ownership/registry. }
  Result := PlainTree(App) and DirExists(App);
  Code := 1;
  Launcher := FindLauncher(App, Root);
  if Result then
    Result := Exec(Launcher,
      'preflight-uninstall --quiet --root "' + Root + '" --app "' + App + '"',
      '', SW_HIDE, ewWaitUntilTerminated, Code);
  Result := Result and (Code = 0);
  if Result then begin
    Code := 1;
    Params := 'uninstall --quiet --root "' + Root + '" --inno';
    if ExpandConstant('{param:DELETESAVES|0}') = '1' then Params := Params + ' --delete-saves';
    Result := Exec(Launcher,
      Params,
      '', SW_SHOWNORMAL, ewWaitUntilTerminated, Code);
    Result := Result and (Code = 0);
  end;
  { Exit 6: the firewall prompt was cancelled or timed out. The rules go first, so nothing was removed. }
  if (not Result) and (Code = 6) then
    MsgBox('Permission wasn''t given, so nothing was removed. Click Uninstall again.', mbInformation, MB_OK)
  else if not Result then
    MsgBox('Uninstall stopped. Your saves are kept.' + #13#10#13#10 + 'To fix it, run:' + #13#10 + 'SoloStone.exe recover --root "' + Root + '"', mbError, MB_OK);
end;
