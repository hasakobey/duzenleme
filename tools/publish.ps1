# NestDesk'i her Windows bilgisayarında çalışacak şekilde paketler.
#
#   powershell -File tools/publish.ps1              # x64 + arm64 + x86, kurulum programı ve taşınabilir zip'ler
#   powershell -File tools/publish.ps1 -Arch x64    # yalnızca bir mimari (hızlı deneme; kurulum yalnızca x64 kabul eder)
#
# Çıktılar (dist/):
#   NestDesk-Setup-<sürüm>.exe                     tek kurulum sihirbazı (İngilizce/Türkçe); bilgisayarın mimarisine uygun sürümü kurar
#   NestDesk-<sürüm>-<mimari>-portable.zip         kurulumsuz kullanım (içinde portable.txt var)
# Program dosyası NestDesk.exe (2.0 ve öncesi Duzenleme.exe; kurulum eskisini siler, bkz. Core/AppInfo.cs).
#
# İsteğe bağlı kod imzalama (imzasız dosyalarda Windows SmartScreen "bilinmeyen yayımcı" uyarısı gösterir,
# Smart App Control açık bilgisayarlar ise çalıştırmayı engelleyebilir). Şunlardan biri ayarlıysa imzalanır
# (her biri eski DUZENLEME_SIGN_* adıyla da okunur):
#   NESTDESK_SIGN_PFX (+ NESTDESK_SIGN_PASSWORD)            .pfx dosyası
#   NESTDESK_SIGN_THUMBPRINT                                sertifika deposundaki/donanım anahtarındaki sertifika
#   NESTDESK_SIGN_DLIB + NESTDESK_SIGN_DMDF                 Microsoft Trusted (Artifact) Signing
param(
    [ValidateSet('x64', 'arm64', 'x86')]
    [string[]]$Arch = @('x64', 'arm64', 'x86'),
    [switch]$SkipInstaller
)
$ErrorActionPreference = 'Stop'
# global.json .NET 10 SDK ister; kullanıcı başına kurulu SDK varsa onu kullan.
$userDotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
if (Test-Path (Join-Path $userDotnet 'dotnet.exe')) { $env:PATH = "$userDotnet;$env:PATH"; $env:DOTNET_ROOT = $userDotnet }

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\Duzenleme\Duzenleme.csproj'
$dist = Join-Path $root 'dist'
$stage = Join-Path $dist 'stage'

$version = ([xml](Get-Content $project -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw 'Duzenleme.csproj içinde <Version> bulunamadı.' }

Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $stage | Out-Null

# --- İmzalama (isteğe bağlı) ---
$timestamp = @('/tr', 'http://timestamp.digicert.com', '/td', 'SHA256', '/fd', 'SHA256')
# NESTDESK_<ad>, yoksa eski adı DUZENLEME_<ad> (uygulamadaki Core/AppEnvironment gibi).
function Get-Setting([string]$name) {
    $value = [Environment]::GetEnvironmentVariable("NESTDESK_$name")
    if (-not $value) { $value = [Environment]::GetEnvironmentVariable("DUZENLEME_$name") }
    return $value
}
$signIdentity = $null
if (Get-Setting 'SIGN_PFX') {
    $signIdentity = @('/f', (Get-Setting 'SIGN_PFX'))
    if (Get-Setting 'SIGN_PASSWORD') { $signIdentity += @('/p', (Get-Setting 'SIGN_PASSWORD')) }
} elseif (Get-Setting 'SIGN_THUMBPRINT') {
    $signIdentity = @('/sha1', (Get-Setting 'SIGN_THUMBPRINT'))
} elseif ((Get-Setting 'SIGN_DLIB') -and (Get-Setting 'SIGN_DMDF')) {
    $signIdentity = @('/dlib', (Get-Setting 'SIGN_DLIB'), '/dmdf', (Get-Setting 'SIGN_DMDF'))
}
$signtool = $null
if ($signIdentity) {
    $signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    if (-not $signtool) { throw 'İmzalama istendi ama signtool bulunamadı (Windows SDK gerekli).' }
}

# Klasördeki imzasız tüm exe/dll'leri imzalar (Microsoft imzalı .NET dosyalarına dokunmaz).
function Sign-Folder([string]$folder) {
    if (-not $signtool) { return }
    $unsigned = @(Get-ChildItem $folder -Recurse -Include *.exe, *.dll | Where-Object { (Get-AuthenticodeSignature $_.FullName).Status -ne 'Valid' })
    for ($i = 0; $i -lt $unsigned.Count; $i += 20) {
        $batch = $unsigned[$i..([Math]::Min($i + 19, $unsigned.Count - 1))] | ForEach-Object FullName
        & $signtool sign @signIdentity @timestamp @batch
        if ($LASTEXITCODE -ne 0) { throw 'İmzalama başarısız.' }
    }
    $still = @(Get-ChildItem $folder -Recurse -Include *.exe, *.dll | Where-Object { (Get-AuthenticodeSignature $_.FullName).Status -ne 'Valid' })
    if ($still.Count -gt 0) { throw "İmzasız dosya kaldı: $($still[0].FullName)" }
}

# --- Her mimari için yayın ---
foreach ($a in $Arch) {
    $out = Join-Path $stage $a
    Write-Host "==> $a yayınlanıyor…"
    # Tek dosya yerine klasör: WPF'in yerel kütüphaneleri %TEMP%'e açılmaz (kilitli bilgisayarlarda engellenmez, hızlı açılır).
    dotnet publish $project -c Release -r "win-$a" --self-contained true `
        -p:PublishSingleFile=false -p:PublishReadyToRun=true -p:DebugType=none -p:GenerateDocumentationFile=false `
        -o $out -nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish başarısız: $a" }
    # Kurulum ve Run değeri bu adı bekler (NestDesk.iss AppExe, Core/AppInfo.cs ExeName).
    if (-not (Test-Path (Join-Path $out 'NestDesk.exe'))) { throw "Yayın çıktısında NestDesk.exe yok: $out" }
    Sign-Folder $out

    # Taşınabilir zip: portable.txt sayesinde ayarlar exe'nin yanındaki data klasöründe tutulur.
    $portable = Join-Path $stage "portable-$a"
    Copy-Item $out $portable -Recurse
    $portableText = @(
        'While this file exists, NestDesk keeps its settings in the "data" folder next to it (portable mode).',
        'Bu dosya varsa NestDesk ayarlarını bu klasördeki "data" klasöründe tutar (taşınabilir kullanım).'
    ) -join "`r`n"
    [IO.File]::WriteAllText((Join-Path $portable 'portable.txt'), $portableText + "`r`n", (New-Object Text.UTF8Encoding $true))
    Compress-Archive -Path (Join-Path $portable '*') -DestinationPath (Join-Path $dist "NestDesk-$version-$a-portable.zip") -CompressionLevel Optimal
}

# --- Kurulum programı ---
if (-not $SkipInstaller) {
    $iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe") |
        Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) { throw 'Inno Setup 6 bulunamadı (https://jrsoftware.org/isdl.php; 6.6 veya üstü).' }

    $defines = @("/DAppVersion=$version", "/DStageDir=$stage", "/DOutputDir=$dist")
    foreach ($a in $Arch) { $defines += "/DHas_$a=1" }
    if ($signtool) {
        # Inno'nun imza komutu: $q tırnak, $f imzalanacak dosya.
        $quote = { param($s) if ($s -match '\s') { "`$q$s`$q" } else { $s } }
        # Inno'da $ özel karakterdir: parolada vb. geçen $ işaretleri $$ olarak kaçırılır.
        $cmd = (@("`$q$signtool`$q", 'sign') + ($signIdentity | ForEach-Object { & $quote ($_ -replace '\$', '$$$$') }) + $timestamp + '$f') -join ' '
        $defines += "/Snestdesksign=$cmd"
        $defines += '/DUseSignTool=1'
    }
    & $iscc @defines /Q (Join-Path $PSScriptRoot 'installer\NestDesk.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Kurulum programı derlenemedi.' }
}

Remove-Item $stage -Recurse -Force
Get-ChildItem $dist | ForEach-Object { '{0,-45} {1,8:N1} MB' -f $_.Name, ($_.Length / 1MB) }
