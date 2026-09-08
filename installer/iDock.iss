; Compile only through scripts/build-installer.ps1, which validates its payload.
#ifndef PackageDir
  #error PackageDir is required
#endif
#ifndef ReleaseDir
  #error ReleaseDir is required
#endif
#ifndef AppVersion
  #define AppVersion "0.5.2"
#endif

[Setup]
AppId={{4C3A1080-B451-4B31-B7D9-08237950642B}
AppName=iDock for Windows
AppVersion={#AppVersion}
AppPublisher=Samuel Rayy
AppPublisherURL=https://github.com/samuelraindrwn/iphone-dock-windows
AppSupportURL=https://github.com/samuelraindrwn/iphone-dock-windows/issues
AppUpdatesURL=https://github.com/samuelraindrwn/iphone-dock-windows/releases
DefaultDirName={autopf}\iDock
DisableDirPage=yes
UsePreviousAppDir=no
UsePreviousTasks=yes
DefaultGroupName=iDock for Windows
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.19041
OutputDir={#ReleaseDir}
OutputBaseFilename=iDock-Setup-{#AppVersion}-win-x64
UninstallDisplayIcon={app}\iDock.exe
SetupIconFile={#PackageDir}\source\iDock\Assets\iDock.ico
Compression=lzma2/normal
SolidCompression=yes
WizardStyle=modern
LicenseFile={#PackageDir}\LICENSE
InfoBeforeFile=setup-information.txt
CloseApplications=no
RestartApplications=no
SetupLogging=yes
UninstallLogging=yes
SetupMutex=Global\iDockInstaller-4C3A1080-B451-4B31-B7D9-08237950642B
VersionInfoVersion={#AppVersion}
VersionInfoDescription=iDock for Windows Setup
VersionInfoProductName=iDock for Windows
VersionInfoProductVersion={#AppVersion}

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked
Name: publicwifi; Description: "Allow AirPlay from the local subnet on ALL Public Wi-Fi networks (not just this Wi-Fi)"; GroupDescription: "Optional network access - enable only if you trust local wireless peers:"; Flags: unchecked

[Files]
; No root data/logs/local helpers enter the validated generated payload.
Source: "{#PackageDir}\*"; DestDir: "{app}"; Excludes: "vendor\uxplay\mDNSResponder.exe,vendor\uxplay\LICENSE.rtf"; Flags: ignoreversion recursesubdirs createallsubdirs
; Bonjour may be registered later by upstream UxPlay and shared with other apps.
; Never remove or replace its executable during upgrade/uninstall. It imports
; only Windows system DLLs. Retain its license alongside the shared executable.
Source: "{#PackageDir}\vendor\uxplay\mDNSResponder.exe"; DestDir: "{app}\vendor\uxplay"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "{#PackageDir}\vendor\uxplay\LICENSE.rtf"; DestDir: "{app}\vendor\uxplay"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "installed.mode"; DestDir: "{app}"; Flags: ignoreversion uninsneveruninstall
Source: "Configure-Firewall.ps1"; DestDir: "{app}\installer"; Flags: ignoreversion
Source: "Configure-Firewall.ps1"; Flags: dontcopy

[Icons]
Name: "{group}\iDock for Windows"; Filename: "{app}\iDock.exe"; WorkingDir: "{app}"
Name: "{group}\Uninstall iDock for Windows"; Filename: "{uninstallexe}"
Name: "{autodesktop}\iDock for Windows"; Filename: "{app}\iDock.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\iDock.exe"; Description: "Open iDock for Windows"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
function GetCurrentProcessId(): LongWord;
  external 'GetCurrentProcessId@kernel32.dll stdcall';

function RunFirewallHelper(const ScriptPath, Mode: String): Boolean;
var
  Code: Integer;
  Args: String;
begin
  Args := '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + ScriptPath +
    '" -Mode ' + Mode + ' -InstallDirectory "' + ExpandConstant('{app}') +
    '" -InstallerProcessId ' + IntToStr(GetCurrentProcessId());
  if (Mode = 'PreflightUninstall') or (Mode = 'Uninstall') then
    Args := Args + ' -UninstallerPath "' + ExpandConstant('{uninstallexe}') + '"';
  if (Mode = 'Preflight') or (Mode = 'Install') then
    if WizardIsTaskSelected('publicwifi') then
      Args := Args + ' -AllowPublicWireless';
  Result := ExecAndLogOutput(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    Args, '', SW_HIDE, ewWaitUntilTerminated, Code, nil) and (Code = 0);
end;

function InitializeUninstall(): Boolean;
begin
  Result := RunFirewallHelper(ExpandConstant('{app}\installer\Configure-Firewall.ps1'), 'PreflightUninstall');
  if not Result then
    SuppressibleMsgBox('Close iDock and its control and AirPlay windows before uninstalling, then try again. ' +
      'No application files or Firewall rules were removed. If this continues, contact support with the uninstall log.',
      mbError, MB_OK, IDOK);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if CompareText(ExpandConstant('{app}'), ExpandConstant('{autopf}\iDock')) <> 0 then begin
    Result := 'Please use the default protected Program Files\iDock directory.';
    exit;
  end;
  if DirExists(ExpandConstant('{app}')) and
     not FileExists(ExpandConstant('{app}\installed.mode')) then begin
    Result := 'Program Files\iDock already contains a folder not managed by this installer. ' +
      'Keep those files safe and choose a separate portable installation or contact support. No files were replaced.';
    exit;
  end;
  ExtractTemporaryFile('Configure-Firewall.ps1');
  if not RunFirewallHelper(ExpandConstant('{tmp}\Configure-Firewall.ps1'), 'Preflight') then
    Result := 'Setup could not safely prepare the application folder or its Firewall rules. ' +
      'Close iDock and UxPlay, then retry. If this persists, review the setup log and ask your administrator to check ' +
      'iDock.AirPlay TCP/UDP Private.v1 and PublicWireless.v1 rules. A modified rule must be reviewed before changing the Public Wi-Fi option. Bonjour and Bluetooth were not changed.';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then begin
    if not RunFirewallHelper(ExpandConstant('{app}\installer\Configure-Firewall.ps1'), 'Install') then
      RaiseException('The application files were installed, but the selected Firewall configuration failed. ' +
        'Run Setup again after reviewing the iDock Firewall rules. Bonjour and Bluetooth were not changed.');
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then begin
    if not RunFirewallHelper(ExpandConstant('{app}\installer\Configure-Firewall.ps1'), 'PreflightUninstall') then
      RaiseException('iDock is still running or the installation path cannot be verified. Close the application and retry uninstall.');
    if not RunFirewallHelper(ExpandConstant('{app}\installer\Configure-Firewall.ps1'), 'Uninstall') then
      SuppressibleMsgBox('Some iDock Firewall rules could not be removed. Ask your administrator to review ' +
        'the iDock.AirPlay Private.v1 and PublicWireless.v1 rules. Other Firewall rules, Bonjour, pairing and user data are preserved.', mbInformation, MB_OK, IDOK);
  end;
end;
