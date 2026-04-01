#define MyAppName "Albion Chat Translator"
#define MyAppVersion "1.1"
#define MyAppPublisher "Tenith"
#define MyAppExeName "AlbionChatTranslator.exe"
#define SourceDir "publish"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\AlbionChatTranslator
DefaultGroupName={#MyAppName}
OutputDir=installer_out
OutputBaseFilename=AlbionChatTranslator_Setup
SetupIconFile=AlbionChatTranslator\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64os
ArchitecturesAllowed=x64os
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
; Desktop shortcut — flagged to always run as administrator
Name: "{autodesktop}\{#MyAppName}";  Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{#MyAppName}";        Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

[Registry]
; Force "Run as administrator" for the exe via AppCompatFlags
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"; \
  ValueType: string; ValueName: "{app}\{#MyAppExeName}"; ValueData: "~ RUNASADMIN"; \
  Flags: uninsdeletevalue

[Code]
// Check whether .NET 9 Windows Desktop Runtime (x64) is installed.
function IsDotNet9DesktopInstalled: Boolean;
var
  FindRec: TFindRec;
  Found: Boolean;
begin
  Found := FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\9.*'), FindRec);
  if Found then FindClose(FindRec);
  Result := Found;
end;

function InitializeSetup: Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if not IsDotNet9DesktopInstalled then
  begin
    if MsgBox(
      '.NET 9 Runtime (x64) is required but not installed.' + #13#10 + #13#10 +
      'Click OK to download the installer, run it, then re-run this setup.',
      mbError, MB_OKCANCEL) = IDOK then
    begin
      ShellExecAsOriginalUser('open', 'https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/9.0.14/windowsdesktop-runtime-9.0.14-win-x64.exe', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    end;
    Result := False;
  end;
end;
