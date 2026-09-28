; NestDesk kurulum sihirbazı (Inno Setup 6.6+). tools/publish.ps1 tarafından derlenir:
;   ISCC /DAppVersion=2.1.0 /DStageDir=<dist\stage> /DOutputDir=<dist> /DHas_x64=1 /DHas_arm64=1 /DHas_x86=1 NestDesk.iss
; Yönetici izni istemez (kullanıcı başına kurulum) ve bilgisayarın mimarisine uygun sürümü kurar. Sihirbaz İngilizce ve
; Türkçedir: dil sorulur (Windows'un dili, güncellemede önceki kurulumun dili seçili gelir; başka dillerde İngilizce).
; 2.1.0'da eski ad (Düzenleme) görünen her yerden kalktı: program dosyası Duzenleme.exe → NestDesk.exe, "Windows ile başlat"
; değeri Duzenleme → NestDesk (kurulum ve uygulama taşır), veri klasörü %AppData%\Duzenleme → %AppData%\NestDesk (uygulama ilk
; açılışta taşır). AppId ve tek örnek adı DEĞİŞMEZ: güncelleme aynı kurulumun üzerine yazılır, çalışan eski sürüm kapatılabilir
; (bkz. Core/AppInfo.cs; tests/Duzenleme.Tests/IdentityTests.cs sabitler).

#if VER < EncodeVer(6,6,0)
  #error Inno Setup 6.6 veya üstü gerekir (WizardStyle=modern dynamic; x64compatible/arm64 tanımlayıcıları).
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

#define AppName "NestDesk"
; Program dosyası (AppInfo.ExeName); 2.0 ve öncesininki güncellemede silinir (AppInfo.LegacyExeName)
#define AppExe "NestDesk.exe"
#define LegacyExe "Duzenleme.exe"
; 2.0.0 öncesi görünen ad: eski kısayollar bu adla
#define LegacyName "Düzenleme"
; "Windows ile başlat" kayıt değerinin adı (AppInfo.RunValueName / LegacyRunValueName)
#define RunValue "NestDesk"
#define LegacyRunValue "Duzenleme"
; %AppData% altındaki veri klasörü (AppInfo.DataFolderName / LegacyDataFolderName)
#define DataFolder "NestDesk"
#define LegacyDataFolder "Duzenleme"
#define AppPublisher "hasakobey"
#define AppUrl "https://github.com/hasakobey/duzenleme"
#define RunKey "Software\Microsoft\Windows\CurrentVersion\Run"
; Görev Yöneticisi'nin Başlangıç sekmesindeki açık/kapalı seçimi: Run değeriyle aynı adlı ikili değer
#define ApprovedKey "Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"
; DEĞİŞMEZ: güncellemeler aynı kurulumun üzerine yazılır
#define AppIdGuid "8F3A1C2E-7B4D-4E6A-9C1F-2D5B8E7A4C31"

[Setup]
AppId={{{#AppIdGuid}}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
; Tek değer (dile göre değişmez): nötr İngilizce
VersionInfoDescription={#AppName} Setup
VersionInfoCompany={#AppPublisher}
VersionInfoCopyright=© 2026 {#AppPublisher}

; Kullanıcı başına kurulum: UAC sorusu yok, standart kullanıcılar da kurabilir.
; Yeni kurulum her zaman %LOCALAPPDATA%\Programs\NestDesk'e gider: kullanıcı yanlışlıkla dolu bir klasör seçemez.
; Güncelleme önceki kurulumun klasöründe kalır (Inno varsayılanı UsePreviousAppDir): 1.x'ten gelenler Programs\Duzenleme'de.
PrivilegesRequired=lowest
DefaultDirName={autopf}\NestDesk
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=no
UsePreviousTasks=yes

; .NET 10'un desteklediği en eski Windows: 10 sürüm 1607.
MinVersion=10.0.14393
ArchitecturesAllowed={#ArchAllowed}
ArchitecturesInstallIn64BitMode=x64compatible or arm64

OutputDir={#OutputDir}
OutputBaseFilename=NestDesk-Setup-{#AppVersion}
SetupIconFile=..\..\src\Duzenleme\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
; Windows'un açık/koyu temasına uyar.
WizardStyle=modern dynamic
WizardSizePercent=110
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
; Önce uygulamaya "kapan" sinyali gönderilir (PrepareToInstall). Son çare olarak Yeniden Başlatma Yöneticisi,
; kurulum klasöründeki dosyaları ([Files] ve [InstallDelete]) kullanan uygulamayı kapatır; klasör dışındaki eski kopyalar
; için kullanıcıya sorulur.
CloseApplications=yes
RestartApplications=no
; Dil her seferinde sorulur (kullanıcı iki dili de görsün); Windows'un ya da önceki kurulumun dili seçili gelir.
ShowLanguageDialog=yes
; Aynı anda iki kurulum çalışmasın.
SetupMutex=NestDeskSetup

#ifdef UseSignTool
SignTool=nestdesksign
SignedUninstaller=yes
#endif

[Languages]
; İlk dil, Windows'un dili listede yoksa varsayılandır (ör. Almanca Windows'ta İngilizce).
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[CustomMessages]
english.StartupTask=Start NestDesk when Windows starts (recommended)
turkish.StartupTask=Windows açıldığında NestDesk'i başlat (önerilir)
english.LaunchApp=Launch NestDesk now
turkish.LaunchApp=NestDesk'i şimdi başlat
english.DeleteDataPrompt=Also delete your settings, notes, widget layout and move history?%n%nChoose No to keep them for a future reinstall. (Folder icons live inside the folders and keep working.)
turkish.DeleteDataPrompt=Ayarların, notların, widget düzenin ve taşıma geçmişin de silinsin mi?%n%nHayır dersen yeniden kurduğunda kaldığın yerden devam edersin. (Klasörlere verdiğin simgeler klasörlerin içinde durur ve çalışmaya devam eder.)
english.BoxFilesKept=Files you put into shortcut boxes ("Items I add to boxes leave the desktop") are in this folder:%n%n%1%n%nNestDesk does not delete them. You can move them back to the desktop yourself.
turkish.BoxFilesKept=Kısayol kutularına koyduğun dosyalar ("Kutulara eklediklerim masaüstünden kalksın") şu klasörde duruyor:%n%n%1%n%nNestDesk onları silmez; istersen masaüstüne kendin geri taşıyabilirsin.
english.Tagline=A tidy home for your desktop.
turkish.Tagline=Masaüstün için derli toplu bir yuva.
english.OtherCopyRunning=Another copy of NestDesk (an older or portable version) is running.%n%nRight-click its tray icon next to the clock, choose "Exit" ("Çıkış" in the Turkish version), then press Retry.
turkish.OtherCopyRunning=NestDesk'in başka bir kopyası (eski sürüm ya da taşınabilir) çalışıyor.%n%nSaatin yanındaki tepsi simgesine sağ tıklayıp "Çıkış"ı (İngilizce sürümde "Exit") seç, sonra "Yeniden dene"ye bas.
english.OtherCopyAbort=Setup was cancelled because NestDesk is still running. Close it and run setup again.
turkish.OtherCopyAbort=NestDesk hâlâ çalıştığı için kurulum iptal edildi. Kapatıp kurulumu yeniden çalıştırabilirsin.
english.QuickAdd=Add widget
turkish.QuickAdd=Widget ekle
english.NoPayload=This setup does not contain files for this computer's processor architecture.
turkish.NoPayload=Bu kurulum programı bu bilgisayarın işlemci mimarisi için dosya içermiyor.

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

[InstallDelete]
; 2.1.0: program dosyası Duzenleme.exe → NestDesk.exe. Eski dosyalar kalırsa eski bir kısayol ya da sabitleme eski sürümü
; (eski exe + eski dll) başlatırdı (Core/LegacyFiles.ProgramFiles ile aynı liste).
Type: files; Name: "{app}\{#LegacyExe}"
Type: files; Name: "{app}\Duzenleme.dll"
Type: files; Name: "{app}\Duzenleme.deps.json"
Type: files; Name: "{app}\Duzenleme.runtimeconfig.json"
; "Widget ekle" kısayolunun adı kurulum diline bağlı: iki dildeki hâli de silinir, şimdiki dilinkini [Icons] yeniden oluşturur
; (öbür dilde kurulmuş eski sürümün kısayolu eski exe'yi gösterip kırık kalmasın).
Type: files; Name: "{autoprograms}\{#AppName} - Widget ekle.lnk"
Type: files; Name: "{autoprograms}\{#AppName} - Add widget.lnk"
Type: files; Name: "{autoprograms}\{#AppName} - Add a widget.lnk"
; 2.0.0 öncesi (Düzenleme) adlı Başlat kısayolları.
Type: files; Name: "{autoprograms}\{#LegacyName}.lnk"
Type: files; Name: "{autoprograms}\{#LegacyName} - Widget ekle.lnk"
Type: files; Name: "{autoprograms}\{#LegacyName} - Add a widget.lnk"
; Masaüstü kısayolları eski exe'yi gösteriyorsa silinir; masaüstü kısayolu seçiliyse [Icons] yenisini oluşturur.
Type: files; Name: "{autodesktop}\{#LegacyName}.lnk"; Check: ShortcutToLegacyExe(ExpandConstant('{autodesktop}\{#LegacyName}.lnk'))
Type: files; Name: "{autodesktop}\{#AppName}.lnk"; Check: ShortcutToLegacyExe(ExpandConstant('{autodesktop}\{#AppName}.lnk'))

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; Comment: "{cm:Tagline}"
; Başlat menüsünden tek tıkla "Widget ekle" penceresi (uygulama çalışıyorsa ona iletilir).
Name: "{autoprograms}\{#AppName} - {cm:QuickAdd}"; Filename: "{app}\{#AppExe}"; Parameters: "--add"; Comment: "{cm:QuickAdd}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Comment: "{cm:Tagline}"; Tasks: desktopicon

[Registry]
; "Windows ile başlat" görevi seçildiyse (uygulamanın Ayarlar'daki anahtarıyla aynı değer).
; Kaldırırken değer yalnızca bu kurulumu gösteriyorsa silinir ([Code]: DeleteOurRunValues); taşınabilir kopyanınkine dokunulmaz.
Root: HKCU; Subkey: "{#RunKey}"; ValueType: string; ValueName: "{#RunValue}"; ValueData: """{app}\{#AppExe}"" --minimized"; Tasks: startup

[UninstallDelete]
; Eski tek dosyalık sürümün geçici klasöre açtığı dosyalar.
Type: filesandordirs; Name: "{localappdata}\Temp\.net\Duzenleme"

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Uygulama zorla kapatılıp Windows masaüstü simgelerini gizli bıraktıysa geri açar. Bu kip tek örnek kilidini almaz ve
; çalışan örneğe sinyal göndermez (uygulama bu noktada zaten kapatılmıştır: CurUninstallStepChanged).
Filename: "{app}\{#AppExe}"; Parameters: "--restore-desktop"; Flags: runhidden skipifdoesntexist; RunOnceId: "RestoreDesktop"

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
{ DEĞİŞMEZ: AppInfo.InstanceIdPrefix. 1.x, 2.0 (Duzenleme.exe) ve 2.1+ (NestDesk.exe) aynı adı kullanır: kurulum eski sürümü
  de bu sinyalle kapatır. }
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

{ Bu kurulum klasöründen çalışan bir NestDesk var mı? Süreç adı exe adıdır: 2.1+ NestDesk, 2.0 ve öncesi Duzenleme.
  Taşınabilir kopyalar ayrı tutulur. }
function InstalledAppRunning(): Boolean;
begin
  Result := RunPowerShell('if (Get-Process NestDesk,Duzenleme -ErrorAction SilentlyContinue | Where-Object { $_.Path -like ''' +
    QuotedAppDir() + '\*'' }) { exit 0 } else { exit 1 }') = 0;
end;

{ Son çare: yalnızca bu kurulum klasöründen çalışan NestDesk'i (yeni ya da eski exe adıyla) kapat; taşınabilir kopyalara dokunma. }
procedure KillInstalledApp();
begin
  RunPowerShell('Get-Process NestDesk,Duzenleme -ErrorAction SilentlyContinue | Where-Object { $_.Path -like ''' +
    QuotedAppDir() + '\*'' } | Stop-Process -Force');
  Sleep(500);
end;

{ Uygulamanın ayar dosyası: önce 2.1'in klasörü, yoksa uygulamanın henüz taşımadığı eski klasör. }
function SettingsFile(): String;
begin
  Result := ExpandConstant('{userappdata}\{#DataFolder}\settings.json');
  if not FileExists(Result) then
    Result := ExpandConstant('{userappdata}\{#LegacyDataFolder}\settings.json');
end;

{ "Kutulara eklediklerim masaüstünden kalksın" kipinde kutulara taşınan dosyaların klasörü: masaüstü klasörünün üst
  klasöründeki NestDesk (OneDrive'a yönlendirilmiş masaüstünde OneDrive'ın içinde); masaüstü bir sürücünün köküyse kullanıcı
  klasörü (Core/BoxPlan.RootFor ile aynı kural). }
function BoxFolder(): String;
var
  Desktop, Parent: String;
begin
  Desktop := RemoveBackslash(ExpandConstant('{userdesktop}'));
  Parent := ExtractFileDir(Desktop);
  if (Length(Desktop) <= 3) or (Parent = '') or (CompareText(Parent, Desktop) = 0) then
    Parent := GetEnv('USERPROFILE');
  Result := AddBackslash(Parent) + '{#AppName}';
end;

{ Klasör var ve içinde en az bir öğe var mı? }
function FolderHasEntries(const Dir: String): Boolean;
var
  FindRec: TFindRec;
begin
  Result := False;
  if not DirExists(Dir) then Exit;
  if FindFirst(AddBackslash(Dir) + '*', FindRec) then
  begin
    try
      repeat
        if (FindRec.Name <> '.') and (FindRec.Name <> '..') then Result := True;
      until Result or (not FindNext(FindRec));
    finally
      FindClose(FindRec);
    end;
  end;
end;

{ Uygulama zorla kapatıldıysa ve masaüstü simgelerini gizlemişse geri aç. }
procedure RestoreDesktopIconsIfHidden();
var
  Settings: AnsiString;
  Progman, DefView, WorkerW, ListView: HWND;
begin
  if not LoadStringFromFile(SettingsFile(), Settings) then Exit;
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

{ ---------- Kısayollar ---------- }
{ Kısayolun hedefi (Windows Script Host ile); okunamazsa boş. }
function ShortcutTarget(const Path: String): String;
var
  Shell, Link: Variant;
begin
  Result := '';
  if not FileExists(Path) then Exit;
  try
    Shell := CreateOleObject('WScript.Shell');
    Link := Shell.CreateShortcut(Path);
    Result := Link.TargetPath;
  except
    Result := '';
  end;
end;

{ Kısayol bu kurulumdaki eski program dosyasını (Duzenleme.exe) mı gösteriyor? ([InstallDelete] denetimi.) }
function ShortcutToLegacyExe(Path: String): Boolean;
begin
  Result := CompareText(ShortcutTarget(Path), ExpandConstant('{app}\{#LegacyExe}')) = 0;
end;

{ ---------- "Windows ile başlat" (Run değeri) ---------- }
{ Run komutundaki exe yolu: tırnaklıysa tırnak içi, değilse ".exe"ye kadar (Core/RunValueMigration.ExePath). }
function RunCommandExe(Value: String): String;
var
  P: Integer;
begin
  Value := Trim(Value);
  if (Length(Value) > 0) and (Value[1] = '"') then
  begin
    Delete(Value, 1, 1);
    P := Pos('"', Value);
    if P > 0 then Value := Copy(Value, 1, P - 1);
  end
  else
  begin
    P := Pos('.exe', Lowercase(Value));
    if P > 0 then Value := Copy(Value, 1, P + 3);
  end;
  Result := Trim(Value);
end;

{ Adı verilen Run değeri bu klasördeki bir programı mı başlatıyor? Klasöre göre karşılaştırılır: eski değer eski exe adını gösterir. }
function RunValuePointsInto(const Name, Dir: String): Boolean;
var
  Value: String;
begin
  Result := RegQueryStringValue(HKCU, '{#RunKey}', Name, Value) and
    (CompareText(RemoveBackslash(ExtractFileDir(RunCommandExe(Value))), RemoveBackslash(Dir)) = 0);
end;

{ 2.0 → 2.1: bu kurulumu gösteren "Duzenleme" değeri "NestDesk" olur (yeni exe'yle); Görev Yöneticisi'ndeki açık/kapalı seçimi
  (StartupApproved) yorumlanmadan taşınır. Uygulamadaki Core/RunValueMigration.Plan ile aynı kurallar: yeni ad başka bir kopyanın
  (ör. başka klasördeki taşınabilir sürüm) ise ona dokunulmaz, eski adlı değer yeni exe'yi gösterir. }
procedure MigrateRunValue(const Dir: String);
var
  Current, Command: String;
  Approved, Existing: AnsiString;
  CurrentIsOurs: Boolean;
begin
  if not RunValuePointsInto('{#LegacyRunValue}', Dir) then Exit;
  Command := '"' + AddBackslash(Dir) + '{#AppExe}" --minimized';
  CurrentIsOurs := RunValuePointsInto('{#RunValue}', Dir);
  if (not CurrentIsOurs) and RegQueryStringValue(HKCU, '{#RunKey}', '{#RunValue}', Current) then
  begin
    RegWriteStringValue(HKCU, '{#RunKey}', '{#LegacyRunValue}', Command);
    Exit;
  end;
  { Önce yeni değer ve seçim yazılır, sonra eskiler silinir: yarıda kalırsa hiçbiri kaybolmaz. }
  RegWriteStringValue(HKCU, '{#RunKey}', '{#RunValue}', Command);
  if RegQueryBinaryValue(HKCU, '{#ApprovedKey}', '{#LegacyRunValue}', Approved) then
  begin
    if not (CurrentIsOurs and RegQueryBinaryValue(HKCU, '{#ApprovedKey}', '{#RunValue}', Existing)) then
      RegWriteBinaryValue(HKCU, '{#ApprovedKey}', '{#RunValue}', Approved);
    RegDeleteValue(HKCU, '{#ApprovedKey}', '{#LegacyRunValue}');
  end;
  RegDeleteValue(HKCU, '{#RunKey}', '{#LegacyRunValue}');
end;

{ Kaldırırken: bu kurulumu gösteren değer(ler)i ve Görev Yöneticisi seçimlerini sil (taşınabilir kopyanınkine dokunma). }
procedure DeleteOurRunValues(const Dir: String);
begin
  if RunValuePointsInto('{#RunValue}', Dir) then
  begin
    RegDeleteValue(HKCU, '{#RunKey}', '{#RunValue}');
    RegDeleteValue(HKCU, '{#ApprovedKey}', '{#RunValue}');
  end;
  if RunValuePointsInto('{#LegacyRunValue}', Dir) then
  begin
    RegDeleteValue(HKCU, '{#RunKey}', '{#LegacyRunValue}');
    RegDeleteValue(HKCU, '{#ApprovedKey}', '{#LegacyRunValue}');
  end;
end;

{ İlk kurulumda "Windows ile başlat" seçilmediyse, eski tek dosyalık sürümden kalan ve başka yeri gösteren başlangıç kaydı
  silinir (yoksa her açılışta eski sürüm başlar ve kurulan sürümü engeller). Taşınabilir kopyanın kaydı (exe'nin yanında
  portable.txt olan) korunur. }
procedure DeleteStaleRunValue(const Name: String);
var
  Value: String;
begin
  if not RegQueryStringValue(HKCU, '{#RunKey}', Name, Value) then Exit;
  if RunValuePointsInto(Name, ExpandConstant('{app}')) then Exit;
  if not FileExists(AddBackslash(ExtractFileDir(RunCommandExe(Value))) + 'portable.txt') then
    RegDeleteValue(HKCU, '{#RunKey}', Name);
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
begin
  if CurStep = ssPostInstall then
  begin
    { Sessiz güncellemede uygulama bir sonraki oturum açılışına dek başlamayabilir: Run değeri burada da taşınır. }
    MigrateRunValue(ExpandConstant('{app}'));
    if FreshInstall and (not WizardIsTaskSelected('startup')) then
    begin
      DeleteStaleRunValue('{#RunValue}');
      DeleteStaleRunValue('{#LegacyRunValue}');
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir, LegacyDataDir, BoxDir: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    { Yalnızca bu kurulum çalışıyorsa kapatılır: aynı anda açık taşınabilir kopya etkilenmez. }
    if InstalledAppRunning() and (not SignalExit()) then
    begin
      KillInstalledApp();
      RestoreDesktopIconsIfHidden();
    end;
    DeleteOurRunValues(ExpandConstant('{app}'));
  end;
  if CurUninstallStep = usPostUninstall then
  begin
    { Kutulara taşınan dosyalar masaüstüne kendiliğinden dönmez ve silinmez: nerede oldukları söylenir (veri klasörü
      silinirse "Hepsini masaüstüne geri koy" kaydı da gider). }
    BoxDir := BoxFolder();
    if FolderHasEntries(BoxDir) and (not UninstallSilent) then
      MsgBox(FmtMessage(CustomMessage('BoxFilesKept'), [BoxDir]), mbInformation, MB_OK);
    { Uygulama eski klasörü taşıyamadıysa veriler hâlâ orada olabilir: soru ikisini birlikte kapsar. }
    DataDir := ExpandConstant('{userappdata}\{#DataFolder}');
    LegacyDataDir := ExpandConstant('{userappdata}\{#LegacyDataFolder}');
    if (DirExists(DataDir) or DirExists(LegacyDataDir)) and (not UninstallSilent) then
      if MsgBox(CustomMessage('DeleteDataPrompt'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      begin
        DelTree(DataDir, True, True, True);
        DelTree(LegacyDataDir, True, True, True);
      end;
  end;
end;
