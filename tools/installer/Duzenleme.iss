; Düzenleme kurulum sihirbazı (Inno Setup 6.3+). tools/publish.ps1 tarafından derlenir:
;   ISCC /DAppVersion=1.1.0 /DStageDir=<dist\stage> /DOutputDir=<dist> /DHas_x64=1 /DHas_arm64=1 /DHas_x86=1 Duzenleme.iss
; Yönetici izni istemez (kullanıcı başına kurulum) ve bilgisayarın mimarisine uygun sürümü kurar.

#if VER < EncodeVer(6,3,0)
  #error Inno Setup 6.3 veya üstü gerekir (x64compatible/arm64 tanımlayıcıları, IsX64Compatible).
#endif

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef StageDir
  #define StageDir "..\..\dist\stage"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\dist"
#endif

; Kurulumun kabul ettiği mimariler, pakete gerçekten konan sürümlerden türetilir.
; x86 sürümü her Windows'ta (x64 ve ARM64 dahil, öykünmeyle) çalışır.
#if Defined(Has_x86)
  #define ArchAllowed "x86compatible"
#elif Defined(Has_x64) && Defined(Has_arm64)
  #define ArchAllowed "x64compatible or arm64"
#elif Defined(Has_x64)
  #define ArchAllowed "x64compatible"
#elif Defined(Has_arm64)
  #define ArchAllowed "arm64"
#else
  #error En az bir mimari (Has_x64 / Has_arm64 / Has_x86) tanımlanmalı.
#endif

#define AppName "Düzenleme"
#define AppExe "Duzenleme.exe"
#define AppPublisher "hasakobey"
#define AppUrl "https://github.com/hasakobey/duzenleme"
#define RunKey "Software\Microsoft\Windows\CurrentVersion\Run"
#define AppIdGuid "8F3A1C2E-7B4D-4E6A-9C1F-2D5B8E7A4C31"

[Setup]
; AppId sabit kalmalı: güncellemeler aynı kurulumun üzerine yazılır.
AppId={{{#AppIdGuid}}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} kurulum programı
VersionInfoCompany={#AppPublisher}
VersionInfoCopyright=© 2026 {#AppPublisher}

; Kullanıcı başına kurulum: UAC sorusu yok, standart kullanıcılar da kurabilir.
; Klasör her zaman %LOCALAPPDATA%\Programs\Duzenleme: kullanıcı yanlışlıkla dolu bir klasör seçemez.
PrivilegesRequired=lowest
DefaultDirName={autopf}\Duzenleme
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=no
UsePreviousTasks=yes

; .NET 10'un desteklediği en eski Windows: 10 sürüm 1607.
MinVersion=10.0.14393
ArchitecturesAllowed={#ArchAllowed}
ArchitecturesInstallIn64BitMode=x64compatible or arm64

OutputDir={#OutputDir}
OutputBaseFilename=Duzenleme-Kurulum-{#AppVersion}
SetupIconFile=..\..\src\Duzenleme\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
WizardSizePercent=110
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
; Önce uygulamaya "kapan" sinyali gönderilir (PrepareToInstall). Son çare olarak Yeniden Başlatma Yöneticisi,
; kurulum klasöründeki dosyaları kullanan uygulamayı kapatır; klasör dışındaki eski kopyalar için kullanıcıya sorulur.
CloseApplications=yes
RestartApplications=no
ShowLanguageDialog=auto

#ifdef UseSignTool
SignTool=duzenlemesign
SignedUninstaller=yes
#endif

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
turkish.StartupTask=Windows açıldığında Düzenleme'yi başlat (önerilir)
english.StartupTask=Start Düzenleme when Windows starts (recommended)
turkish.LaunchApp=Düzenleme'yi şimdi başlat
english.LaunchApp=Launch Düzenleme now
turkish.DeleteDataPrompt=Ayarların, notların, widget düzenin ve taşıma geçmişin de silinsin mi?%n%nHayır dersen yeniden kurduğunda kaldığın yerden devam edersin. (Klasörlere verdiğin simgeler klasörlerin içinde durur ve çalışmaya devam eder.)
english.DeleteDataPrompt=Also delete your settings, notes, widget layout and move history?%n%nChoose No to keep them for a future reinstall. (Folder icons live inside the folders and keep working.)
turkish.Tagline=Masaüstünü düzenleyen, saat/tarih/not widget'ları sunan modern düzenleyici.
english.Tagline=A modern desktop organizer with clock, date and note widgets.
turkish.OtherCopyRunning=Düzenleme'nin başka bir kopyası (eski sürüm ya da taşınabilir) çalışıyor.%n%nSaatin yanındaki tepsi simgesine sağ tıklayıp "Çıkış"ı seç, sonra "Yeniden dene"ye bas.
english.OtherCopyRunning=Another copy of Düzenleme (an older or portable version) is running.%n%nRight-click its tray icon next to the clock, choose "Çıkış" (Exit), then press Retry.
turkish.OtherCopyAbort=Düzenleme hâlâ çalıştığı için kurulum iptal edildi. Kapatıp kurulumu yeniden çalıştırabilirsin.
english.OtherCopyAbort=Setup was cancelled because Düzenleme is still running. Close it and run setup again.
turkish.QuickAdd=Widget ekle
english.QuickAdd=Add a widget
turkish.NoPayload=Bu kurulum programı bu bilgisayarın işlemci mimarisi için dosya içermiyor.
english.NoPayload=This setup does not contain files for this computer's processor architecture.

[Tasks]
; Yalnızca ilk kurulumda sorulur: güncellemeler kullanıcının uygulama içindeki tercihini değiştirmez.
Name: "startup"; Description: "{cm:StartupTask}"; Check: not IsUpgrade
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Mimariye göre tek sürüm kurulur (x64 önde: katı sıkıştırmada en yaygın sürüm en hızlı açılır).
#ifdef Has_x64
Source: "{#StageDir}\x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: InstallArch('x64')
#endif
#ifdef Has_arm64
Source: "{#StageDir}\arm64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: InstallArch('arm64')
#endif
#ifdef Has_x86
Source: "{#StageDir}\x86\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: InstallArch('x86')
#endif

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; Comment: "{cm:Tagline}"
; Başlat menüsünden tek tıkla "Widget ekle" penceresi (uygulama çalışıyorsa ona iletilir).
Name: "{autoprograms}\{#AppName} - {cm:QuickAdd}"; Filename: "{app}\{#AppExe}"; Parameters: "--add"; Comment: "{cm:QuickAdd}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Comment: "{cm:Tagline}"; Tasks: desktopicon

[Registry]
; "Windows ile başlat" görevi seçildiyse (uygulamanın Ayarlar'daki anahtarıyla aynı değer).
; Kaldırırken değer yalnızca bu kurulumu gösteriyorsa silinir ([Code]: RunValueIsOurs); taşınabilir kopyanınkine dokunulmaz.
Root: HKCU; Subkey: "{#RunKey}"; ValueType: string; ValueName: "Duzenleme"; ValueData: """{app}\{#AppExe}"" --minimized"; Tasks: startup

[UninstallDelete]
; Eski tek dosyalık sürümün geçici klasöre açtığı dosyalar.
Type: filesandordirs; Name: "{localappdata}\Temp\.net\Duzenleme"

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[Code]
const
  EVENT_MODIFY_STATE = $0002;

function OpenEvent(DesiredAccess: DWORD; InheritHandle: BOOL; Name: String): THandle;
  external 'OpenEventW@kernel32.dll stdcall';
function SetEvent(Handle: THandle): BOOL;
  external 'SetEvent@kernel32.dll stdcall';
function CloseHandle(Handle: THandle): BOOL;
  external 'CloseHandle@kernel32.dll stdcall';
function FindWindowEx(Parent, ChildAfter: HWND; ClassName, WindowName: String): HWND;
  external 'FindWindowExW@user32.dll stdcall';
function ShowWindow(Wnd: HWND; CmdShow: Integer): BOOL;
  external 'ShowWindow@user32.dll stdcall';

{ ---------- Mimari seçimi: pakette ne varsa, en uygun olan ---------- }
function PickArch(): String;
begin
  Result := '';
#ifdef Has_arm64
  if IsArm64 then begin Result := 'arm64'; Exit; end;          { ARM64 Windows'ta yerel sürüm }
#endif
#ifdef Has_x64
  if IsX64Compatible then begin Result := 'x64'; Exit; end;     { x64 Windows ya da Windows 11 ARM64 öykünmesi }
#endif
#ifdef Has_x86
  Result := 'x86';                                              { 32-bit Windows; son çare olarak her yerde }
#endif
end;

function InstallArch(Arch: String): Boolean;
begin
  Result := (Arch = PickArch());
end;

function InitializeSetup(): Boolean;
begin
  Result := PickArch() <> '';
  if not Result then
    MsgBox(CustomMessage('NoPayload'), mbCriticalError, MB_OK);
end;

function IsUpgrade(): Boolean;
begin
  Result := RegKeyExists(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{{#AppIdGuid}}_is1');
end;

{ ---------- Çalışan uygulamayı kapatma ---------- }
function InstanceId(): String;
begin
  Result := 'Duzenleme.' + GetUserNameString;
end;

function AppRunning(): Boolean;
begin
  Result := CheckForMutexes(InstanceId());
end;

{ Uygulamaya "kapan" sinyali gönderir (eski exe'yi çalıştırmaz); kapanana kadar en çok ~8 sn bekler. }
function SignalExit(): Boolean;
var
  Ev: THandle;
  I: Integer;
begin
  Result := not AppRunning();
  if Result then Exit;
  Ev := OpenEvent(EVENT_MODIFY_STATE, False, InstanceId() + '.exit');
  if Ev <> 0 then
  begin
    SetEvent(Ev);
    CloseHandle(Ev);
  end;
  for I := 1 to 32 do
  begin
    if not AppRunning() then
    begin
      Sleep(800);   { süreç, kilidi bıraktıktan sonra kapanmayı bitirsin }
      Result := True;
      Exit;
    end;
    Sleep(250);
  end;
end;

{ PowerShell komutunda tek tırnak içine gömülecek kurulum klasörü ('' kaçışıyla; klasör adında ' olabilir). }
function QuotedAppDir(): String;
begin
  Result := ExpandConstant('{app}');
  StringChangeEx(Result, '''', '''''', True);
end;

function RunPowerShell(Command: String): Integer;
begin
  if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -NonInteractive -Command "' + Command + '"', '', SW_HIDE, ewWaitUntilTerminated, Result) then
    Result := -1;
end;

{ Bu kurulum klasöründen çalışan bir Düzenleme var mı? (Taşınabilir kopyalar ayrı tutulur.) }
function InstalledAppRunning(): Boolean;
begin
  Result := RunPowerShell('if (Get-Process Duzenleme -ErrorAction SilentlyContinue | Where-Object { $_.Path -like ''' +
    QuotedAppDir() + '\*'' }) { exit 0 } else { exit 1 }') = 0;
end;

{ Son çare: yalnızca bu kurulum klasöründen çalışan Düzenleme'yi kapat (taşınabilir kopyalara dokunma). }
procedure KillInstalledApp();
begin
  RunPowerShell('Get-Process Duzenleme -ErrorAction SilentlyContinue | Where-Object { $_.Path -like ''' +
    QuotedAppDir() + '\*'' } | Stop-Process -Force');
  Sleep(500);
end;

{ Uygulama zorla kapatıldıysa ve masaüstü simgelerini gizlemişse geri aç. }
procedure RestoreDesktopIconsIfHidden();
var
  Settings: AnsiString;
  Progman, DefView, WorkerW, ListView: HWND;
begin
  if not LoadStringFromFile(ExpandConstant('{userappdata}\Duzenleme\settings.json'), Settings) then Exit;
  if Pos('"IconsHiddenByApp": true', Settings) = 0 then Exit;
  Progman := FindWindowEx(0, 0, 'Progman', 'Program Manager');
  DefView := FindWindowEx(Progman, 0, 'SHELLDLL_DefView', '');
  WorkerW := 0;
  while (DefView = 0) do
  begin
    WorkerW := FindWindowEx(0, WorkerW, 'WorkerW', '');
    if WorkerW = 0 then Break;
    DefView := FindWindowEx(WorkerW, 0, 'SHELLDLL_DefView', '');
  end;
  if DefView = 0 then Exit;
  ListView := FindWindowEx(DefView, 0, 'SysListView32', 'FolderView');
  if ListView <> 0 then ShowWindow(ListView, SW_SHOW);
end;

{ "Windows ile başlat" değeri bu kurulumu mu gösteriyor? }
function RunValueIsOurs(): Boolean;
var
  Value: String;
begin
  Result := RegQueryStringValue(HKCU, '{#RunKey}', 'Duzenleme', Value) and
    (Pos(Lowercase(ExpandConstant('{app}\')), Lowercase(Value)) > 0);
end;

var
  FreshInstall: Boolean;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  FreshInstall := not IsUpgrade();
  if SignalExit() then Exit;

  { Sinyali bilmeyen eski sürüm kurulum klasöründen çalışıyorsa kapatılır. }
  KillInstalledApp();
  if AppRunning() then Sleep(1000);
  if not AppRunning() then
  begin
    RestoreDesktopIconsIfHidden();
    Exit;
  end;

  { Klasör dışında bir kopya (indirilmiş eski 1.0 exe'si ya da taşınabilir sürüm) çalışıyor: kullanıcı kapatsın.
    Sessiz kurulumda sorulmaz; kurulum sürer, yeni sürüm diğer kopya kapanınca açılabilir. }
  if WizardSilent then Exit;
  while AppRunning() do
  begin
    if MsgBox(CustomMessage('OtherCopyRunning'), mbError, MB_RETRYCANCEL) = IDCANCEL then
    begin
      Result := CustomMessage('OtherCopyAbort');
      Exit;
    end;
    SignalExit();
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Value: String;
begin
  { İlk kurulumda "Windows ile başlat" seçilmediyse, eski tek dosyalık sürümden kalan ve başka yeri gösteren
    başlangıç kaydı silinir (yoksa her açılışta eski sürüm başlar ve kurulan sürümü engeller).
    Taşınabilir kopyanın kaydı (exe'nin yanında portable.txt olan) korunur. }
  if (CurStep = ssPostInstall) and FreshInstall and (not WizardIsTaskSelected('startup')) and
     RegQueryStringValue(HKCU, '{#RunKey}', 'Duzenleme', Value) and (not RunValueIsOurs()) then
  begin
    StringChangeEx(Value, '"', '', True);
    StringChangeEx(Value, ' --minimized', '', True);
    if not FileExists(AddBackslash(ExtractFileDir(Value)) + 'portable.txt') then
      RegDeleteValue(HKCU, '{#RunKey}', 'Duzenleme');
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    { Yalnızca bu kurulum çalışıyorsa kapatılır: aynı anda açık taşınabilir kopya etkilenmez. }
    if InstalledAppRunning() and (not SignalExit()) then
    begin
      KillInstalledApp();
      RestoreDesktopIconsIfHidden();
    end;
    if RunValueIsOurs() then
      RegDeleteValue(HKCU, '{#RunKey}', 'Duzenleme');
  end;
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{userappdata}\Duzenleme');
    if DirExists(DataDir) and (not UninstallSilent) then
      if MsgBox(CustomMessage('DeleteDataPrompt'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(DataDir, True, True, True);
  end;
end;
