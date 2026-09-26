# Düzenleme'yi her Windows bilgisayarında çalışacak şekilde paketler.
#
#   powershell -File tools/publish.ps1              # x64 + arm64 + x86, kurulum programı ve taşınabilir zip'ler
#   powershell -File tools/publish.ps1 -Arch x64    # yalnızca bir mimari (hızlı deneme; kurulum yalnızca x64 kabul eder)
#
# Çıktılar (dist/):
#   Duzenleme-Kurulum-<sürüm>.exe                  tek kurulum sihirbazı; bilgisayarın mimarisine uygun sürümü kurar
#   Duzenleme-<sürüm>-<mimari>-tasinabilir.zip     kurulumsuz kullanım (içinde portable.txt var)
#
# İsteğe bağlı kod imzalama (imzasız dosyalarda Windows SmartScreen "bilinmeyen yayımcı" uyarısı gösterir,
# Smart App Control açık bilgisayarlar ise çalıştırmayı engelleyebilir). Şunlardan biri ayarlıysa imzalanır:
#   DUZENLEME_SIGN_PFX (+ DUZENLEME_SIGN_PASSWORD)          .pfx dosyası
#   DUZENLEME_SIGN_THUMBPRINT                               sertifika deposundaki/donanım anahtarındaki sertifika
#   DUZENLEME_SIGN_DLIB + DUZENLEME_SIGN_DMDF               Microsoft Trusted (Artifact) Signing
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
$signIdentity = $null
if ($env:DUZENLEME_SIGN_PFX) {
    $signIdentity = @('/f', $env:DUZENLEME_SIGN_PFX)
    if ($env:DUZENLEME_SIGN_PASSWORD) { $signIdentity += @('/p', $env:DUZENLEME_SIGN_PASSWORD) }
} elseif ($env:DUZENLEME_SIGN_THUMBPRINT) {
    $signIdentity = @('/sha1', $env:DUZENLEME_SIGN_THUMBPRINT)
} elseif ($env:DUZENLEME_SIGN_DLIB -and $env:DUZENLEME_SIGN_DMDF) {
    $signIdentity = @('/dlib', $env:DUZENLEME_SIGN_DLIB, '/dmdf', $env:DUZENLEME_SIGN_DMDF)
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
    Sign-Folder $out

    # Taşınabilir zip: portable.txt sayesinde ayarlar exe'nin yanındaki data klasöründe tutulur.
    $portable = Join-Path $stage "portable-$a"
    Copy-Item $out $portable -Recurse
    Set-Content (Join-Path $portable 'portable.txt') 'Bu dosya varsa Düzenleme ayarlarını bu klasördeki "data" klasöründe tutar.' -Encoding UTF8
    Compress-Archive -Path (Join-Path $portable '*') -DestinationPath (Join-Path $dist "Duzenleme-$version-$a-tasinabilir.zip") -CompressionLevel Optimal
}

# --- Kurulum programı ---
if (-not $SkipInstaller) {
    $iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe") |
        Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) { throw 'Inno Setup 6 bulunamadı (https://jrsoftware.org/isdl.php).' }

    $defines = @("/DAppVersion=$version", "/DStageDir=$stage", "/DOutputDir=$dist")
    foreach ($a in $Arch) { $defines += "/DHas_$a=1" }
    if ($signtool) {
        # Inno'nun imza komutu: $q tırnak, $f imzalanacak dosya.
        $quote = { param($s) if ($s -match '\s') { "`$q$s`$q" } else { $s } }
        # Inno'da $ özel karakterdir: parolada vb. geçen $ işaretleri $$ olarak kaçırılır.
        $cmd = (@("`$q$signtool`$q", 'sign') + ($signIdentity | ForEach-Object { & $quote ($_ -replace '\$', '$$$$') }) + $timestamp + '$f') -join ' '
        $defines += "/Sduzenlemesign=$cmd"
        $defines += '/DUseSignTool=1'
    }
    & $iscc @defines /Q (Join-Path $PSScriptRoot 'installer\Duzenleme.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Kurulum programı derlenemedi.' }
}

Remove-Item $stage -Recurse -Force
Get-ChildItem $dist | ForEach-Object { '{0,-45} {1,8:N1} MB' -f $_.Name, ($_.Length / 1MB) }
