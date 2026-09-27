# NestDesk'i Microsoft Store'a gönderilecek MSIX paketi olarak hazırlar (Store paketi kendisi imzalar).
#
#   powershell -File tools/package-msix.ps1               # x64 + arm64 + x86, tek .msixbundle
#   powershell -File tools/package-msix.ps1 -Arch x64     # yalnızca bir mimari (hızlı deneme)
#   powershell -File tools/package-msix.ps1 -Strict       # identity.json'da yer tutucu varsa dur (Store'a gönderirken kullan)
#   powershell -File tools/package-msix.ps1 -TestSign     # yan yükleme denemesi için kendinden imzalı sertifikayla imzala
#
# Çıktılar (dist-store/; dist/ içinde değil, çünkü tools/publish.ps1 her çalıştığında bütün dist/ klasörünü siler):
#   NestDesk-<sürüm>.msixbundle           Partner Center'a yüklenecek dosya
#   NestDesk-DenemeImzasi.pfx / .cer      yalnızca -TestSign ile; paket, .cer yerel makinenin "Güvenilen Kişiler"
#                                         deposuna eklenmeden (yönetici izni gerekir) kurulamaz
# Paketin içindeki program dosyası bilerek Duzenleme.exe kalır (AppxManifest.xml: Executable, Application Id, TaskId).
# Her şey önce geçici klasörde üretilir; dist-store/ içindeki eski çıktı ancak çalıştırma baştan sona başarılı olunca
# yenisiyle değiştirilir (yarıda kalan çalıştırma son sağlam paketi silmez).
#
# Store kimliği tools/store/identity.json'dan, paket bildirimi tools/store/AppxManifest.xml şablonundan gelir; görseller
# src/Duzenleme/Assets/app.ico'dan üretilir. makeappx/makepri/signtool için Windows SDK kurmak gerekmez: sürümü sabit
# Microsoft.Windows.SDK.BuildTools NuGet paketi bir kez %LOCALAPPDATA%\DuzenlemeBuildTools altına indirilir.
param(
    [ValidateSet('x64', 'arm64', 'x86')]
    [string[]]$Arch = @('x64', 'arm64', 'x86'),
    [switch]$Strict,
    [switch]$TestSign
)
$ErrorActionPreference = 'Stop'
# Windows PowerShell'de ilerleme çubuğu indirmeyi çok yavaşlatır.
$ProgressPreference = 'SilentlyContinue'
# global.json .NET 10 SDK ister; kullanıcı başına kurulu SDK varsa onu kullan.
$userDotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
if (Test-Path (Join-Path $userDotnet 'dotnet.exe')) { $env:PATH = "$userDotnet;$env:PATH"; $env:DOTNET_ROOT = $userDotnet }

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\Duzenleme\Duzenleme.csproj'
$storeSrc = Join-Path $PSScriptRoot 'store'
$outDir = Join-Path $root 'dist-store'

# nuget.org'daki paketler değiştirilemez; özet yine de indirilen dosyayı doğrular. Güncellerken ikisini birlikte değiştir.
$buildToolsVersion = '10.0.28000.2705'
$buildToolsSha256 = '8BFDFB6CA2633F531CF80B5FA22512BA61A394D7988F0970DB83BAADC67929ED'

# --- Sürüm: Store dört parça ister ve son parça 0 olmalı ---
$version = ([xml](Get-Content $project -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Duzenleme.csproj içindeki <Version> üç parçalı olmalı (ör. 1.4.1); bulunan: '$version'." }
$packageVersion = "$version.0"

# --- Store kimliği ---
$identity = [IO.File]::ReadAllText((Join-Path $storeSrc 'identity.json'), [Text.Encoding]::UTF8) | ConvertFrom-Json
foreach ($key in 'Name', 'Publisher', 'PublisherDisplayName') {
    if (-not $identity.$key) { throw "tools/store/identity.json içinde '$key' eksik." }
}
if ($identity.Name -notmatch '^[A-Za-z0-9.-]{3,50}$') { throw "identity.json Name geçersiz: '$($identity.Name)' (3-50 karakter; harf, rakam, nokta, tire)." }
if ($identity.Publisher -notmatch '^CN=') { throw "identity.json Publisher 'CN=' ile başlamalı: '$($identity.Publisher)'." }
$placeholders = @(
    if ($identity.Name -like 'YERTUTUCU*') { 'Name' }
    if ($identity.Publisher -eq 'CN=00000000-0000-0000-0000-000000000000') { 'Publisher' }
    if ($identity.PublisherDisplayName -like 'YERTUTUCU*') { 'PublisherDisplayName' }
)
$isPlaceholder = $placeholders.Count -gt 0
$placeholderMessage = "tools/store/identity.json yer tutucu değerler içeriyor ($($placeholders -join ', ')); bu paket Store'a yüklenemez, " +
    'yalnızca deneme içindir. Değerleri Partner Center > Ürün kimliği sayfasından al.'
if ($isPlaceholder) {
    if ($Strict) { throw $placeholderMessage }
    Write-Warning $placeholderMessage
}

$work = Join-Path ([IO.Path]::GetTempPath()) ('duzenleme-msix-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force $work | Out-Null
# Çıktılar burada toplanır; $outDir'e yalnızca her şey bitince taşınır.
$staging = Join-Path $work 'cikti'
New-Item -ItemType Directory -Force $staging | Out-Null

# Yerel aracı çalıştırır. makeappx paketlediği her dosyayı yazdığı için çıktı yalnızca hata olursa gösterilir;
# uyarılar görünür kalır. (makepri UTF-16 yazar: araya giren NUL karakterleri atılır.)
function Invoke-Tool([string]$exe, [string[]]$arguments) {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { $output = @(& $exe @arguments 2>&1 | ForEach-Object { "$_" -replace "`0", '' }) }
    finally { $ErrorActionPreference = $previous }
    if ($LASTEXITCODE -ne 0) {
        $output | Where-Object { $_.Trim() } | ForEach-Object { Write-Host $_ }
        throw "$([IO.Path]::GetFileName($exe)) başarısız (çıkış kodu $LASTEXITCODE)."
    }
    $output | Where-Object { $_ -match '\bwarning\b' } | ForEach-Object { Write-Warning $_.Trim() }
}

# makeappx, makepri ve signtool'un (x64) bulunduğu klasör; ilk çalıştırmada indirilir.
function Get-BuildTools {
    $cache = Join-Path $env:LOCALAPPDATA "DuzenlemeBuildTools\Microsoft.Windows.SDK.BuildTools.$buildToolsVersion"
    $find = {
        Get-ChildItem (Join-Path $cache 'bin') -Directory -ErrorAction SilentlyContinue |
            ForEach-Object { Join-Path $_.FullName 'x64' } |
            Where-Object { Test-Path (Join-Path $_ 'makeappx.exe') } | Select-Object -First 1
    }
    $bin = & $find
    if ($bin) { return $bin }

    Write-Host "==> Microsoft.Windows.SDK.BuildTools $buildToolsVersion indiriliyor (yalnızca ilk sefer)…"
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    $nupkg = Join-Path $work 'buildtools.nupkg'
    $id = 'microsoft.windows.sdk.buildtools'
    Invoke-WebRequest -UseBasicParsing -OutFile $nupkg -Uri "https://api.nuget.org/v3-flatcontainer/$id/$buildToolsVersion/$id.$buildToolsVersion.nupkg"
    $hash = (Get-FileHash $nupkg -Algorithm SHA256).Hash
    if ($hash -ne $buildToolsSha256) { throw "İndirilen araç paketinin SHA-256 özeti beklenenden farklı: $hash" }

    # Yalnızca x64 araçları açılır (~20 MB). Önce yan klasöre açılıp bitince yerine taşınır: yarıda kesilen açma önbellek sanılmaz.
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $partial = "$cache.yarim"
    Remove-Item $partial -Recurse -Force -ErrorAction SilentlyContinue
    $zip = [IO.Compression.ZipFile]::OpenRead($nupkg)
    try {
        foreach ($entry in $zip.Entries) {
            if ($entry.FullName -notmatch '^bin/[^/]+/x64/.*[^/]$') { continue }
            $target = Join-Path $partial ($entry.FullName -replace '/', '\')
            New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
        }
    } finally { $zip.Dispose() }
    Remove-Item $cache -Recurse -Force -ErrorAction SilentlyContinue
    Move-Item $partial $cache
    $bin = & $find
    if (-not $bin) { throw 'Araç paketinde bin\<sürüm>\x64\makeappx.exe bulunamadı.' }
    return $bin
}

# --- Görseller: app.ico'dan ölçek/targetsize nitelikli PNG'ler ---
Add-Type -AssemblyName System.Drawing

# app.ico karelerini kenar uzunluğuna göre döndürür. tools/make-icon.ps1 256'lık kareyi PNG, küçükleri 32 bit DIB yazar;
# başka biçimdeki (paletli/AND maskeli) kareler atlanır, o boyutlar büyük kareden küçültülür.
function Read-IcoFrames([string]$path) {
    $bytes = [IO.File]::ReadAllBytes($path)
    $frames = @{}
    for ($i = 0; $i -lt [BitConverter]::ToUInt16($bytes, 4); $i++) {
        $entry = 6 + 16 * $i
        $length = [BitConverter]::ToInt32($bytes, $entry + 8)
        $offset = [BitConverter]::ToInt32($bytes, $entry + 12)
        $bitmap = $null
        if ($bytes[$offset] -eq 0x89 -and $bytes[$offset + 1] -eq 0x50 -and $bytes[$offset + 2] -eq 0x4E -and $bytes[$offset + 3] -eq 0x47) {
            # Akış kapatılmaz: GDI+ görüntü yaşadıkça ona erişir.
            $bitmap = New-Object System.Drawing.Bitmap (New-Object IO.MemoryStream($bytes, $offset, $length, $false))
        } elseif ([BitConverter]::ToInt32($bytes, $offset) -eq 40 -and [BitConverter]::ToUInt16($bytes, $offset + 14) -eq 32 -and
                  [BitConverter]::ToInt32($bytes, $offset + 16) -eq 0) {
            $size = [BitConverter]::ToInt32($bytes, $offset + 4)
            $bitmap = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $data = $bitmap.LockBits((New-Object System.Drawing.Rectangle 0, 0, $size, $size), 'WriteOnly', $bitmap.PixelFormat)
            # DIB satırları alttan üste; BGRA sırası Format32bppArgb'nin bellek düzeniyle aynı.
            for ($y = 0; $y -lt $size; $y++) {
                $source = $offset + 40 + ($size - 1 - $y) * $size * 4
                [Runtime.InteropServices.Marshal]::Copy($bytes, $source, [IntPtr]($data.Scan0.ToInt64() + $y * $data.Stride), $size * 4)
            }
            $bitmap.UnlockBits($data)
        }
        if ($bitmap -and $bitmap.Width -eq $bitmap.Height) { $frames[$bitmap.Width] = $bitmap }
    }
    return $frames
}

# $source'u $iconSize kenarlı olarak $width x $height saydam tuvalin ortasına çizip PNG kaydeder.
function Save-Png([System.Drawing.Bitmap]$source, [int]$width, [int]$height, [int]$iconSize, [string]$path) {
    if ($source.Width -eq $iconSize -and $width -eq $iconSize -and $height -eq $iconSize) {
        $source.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        return
    }
    $bitmap = New-Object System.Drawing.Bitmap $width, $height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $g.Clear([System.Drawing.Color]::Transparent)
        $g.InterpolationMode = 'HighQualityBicubic'
        $g.PixelOffsetMode = 'HighQuality'
        $g.CompositingQuality = 'HighQuality'
        $g.SmoothingMode = 'HighQuality'
        # Kenar pikselleri tuval dışını örneklemesin (aksi hâlde kenarda yarı saydam hale oluşur).
        $attributes = New-Object System.Drawing.Imaging.ImageAttributes
        $attributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
        # Tam sayı konum: simge piksel ızgarasına oturur, bulanıklaşmaz.
        $x = [int][Math]::Floor(($width - $iconSize) / 2)
        $y = [int][Math]::Floor(($height - $iconSize) / 2)
        $g.DrawImage($source, (New-Object System.Drawing.Rectangle $x, $y, $iconSize, $iconSize),
            0, 0, $source.Width, $source.Height, [System.Drawing.GraphicsUnit]::Pixel, $attributes)
    } finally { $g.Dispose() }
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
}

function New-Assets([string]$dir) {
    New-Item -ItemType Directory -Force $dir | Out-Null
    $frames = Read-IcoFrames (Join-Path $root 'src\Duzenleme\Assets\app.ico')
    $largest = $frames[($frames.Keys | Sort-Object -Descending | Select-Object -First 1)]
    if (-not $largest -or $largest.Width -lt 256) { throw 'app.ico içinde 256x256 kare bulunamadı.' }
    # Kendi karesi olan boyutlar (16/24/32/48/256) doğrudan o kareden: küçültülmüş büyük kareden keskindir.
    $pick = { param([int]$size) if ($frames.ContainsKey($size)) { $frames[$size] } else { $largest } }
    $draw = { param([string]$name, [int]$width, [int]$height, [int]$iconSize) Save-Png (& $pick $iconSize) $width $height $iconSize (Join-Path $dir $name) }

    # Uygulama listesi, görev çubuğu, Başlat: simge tuvali doldurur (app.ico zaten kenarları yuvarlatılmış bir kutucuk).
    foreach ($scale in 100, 125, 150, 200, 400) {
        $s = [int](44 * $scale / 100)
        & $draw "Square44x44Logo.scale-$scale.png" $s $s $s
    }
    # BackgroundColor saydam olduğundan "plated" ve "unplated" biçimler aynı görünür; ikisi de bulunsun.
    foreach ($size in 16, 24, 32, 48, 256) {
        & $draw "Square44x44Logo.targetsize-$size.png" $size $size $size
        & $draw "Square44x44Logo.targetsize-${size}_altform-unplated.png" $size $size $size
    }
    # Kutucuklar (Windows 10 Başlat): simge yarı yükseklikte, ortada, saydam zeminde.
    foreach ($scale in 100, 200, 400) {
        $s = [int](150 * $scale / 100)
        & $draw "Square150x150Logo.scale-$scale.png" $s $s ([int]($s / 2))
    }
    foreach ($scale in 100, 200) {
        $h = [int](150 * $scale / 100)
        & $draw "Wide310x150Logo.scale-$scale.png" ([int](310 * $scale / 100)) $h ([int]($h / 2))
    }
    # Store/Uygulama Yükleyicisi/Ayarlar > Uygulamalar.
    foreach ($scale in 100, 200, 400) {
        $s = [int](50 * $scale / 100)
        & $draw "StoreLogo.scale-$scale.png" $s $s $s
    }
}

# --- Paket bildirimi ---
# Yorumlar pakete girmez (şablondaki {{…}} açıklamaları da doldurulmasın diye önce atılır); değerler XML'e kaçırılır.
$template = [IO.File]::ReadAllText((Join-Path $storeSrc 'AppxManifest.xml'), [Text.Encoding]::UTF8)
$template = [regex]::Replace($template, '(?m)^[ \t]*<!--(?s:.*?)-->[ \t]*\r?\n', '')
function Write-Manifest([string]$arch, [string]$path) {
    $values = @{
        Name = $identity.Name; Publisher = $identity.Publisher; PublisherDisplayName = $identity.PublisherDisplayName
        Version = $packageVersion; Arch = $arch
    }
    $text = $template
    foreach ($key in $values.Keys) { $text = $text.Replace("{{$key}}", [Security.SecurityElement]::Escape($values[$key])) }
    if ($text -match '\{\{\w+\}\}') { throw "AppxManifest.xml şablonunda doldurulmamış belirteç: $($Matches[0])" }
    [void][xml]$text
    New-Item -ItemType Directory -Force (Split-Path $path) | Out-Null
    [IO.File]::WriteAllText($path, $text, (New-Object Text.UTF8Encoding $false))
}

try {
    $tools = Get-BuildTools
    $makeappx = Join-Path $tools 'makeappx.exe'
    $makepri = Join-Path $tools 'makepri.exe'

    # --- İsteğe bağlı deneme imzası ---
    $signArgs = $null
    if ($TestSign) {
        Write-Host '==> Deneme sertifikası oluşturuluyor…'
        # Parola rastgele: sertifika yalnızca bu çalıştırmanın paketlerini imzalar (sonda yazdırılır).
        $random = New-Object byte[] 18
        [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($random)
        $password = [Convert]::ToBase64String($random)
        # Paketin Publisher'ı ile sertifika konusu birebir aynı olmalı; 1.3.6.1.5.5.7.3.3 = kod imzalama.
        $cert = New-SelfSignedCertificate -Type Custom -Subject $identity.Publisher -KeyUsage DigitalSignature `
            -FriendlyName 'NestDesk MSIX deneme imzası' -CertStoreLocation 'Cert:\CurrentUser\My' -NotAfter (Get-Date).AddYears(1) `
            -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
        $pfx = Join-Path $staging 'NestDesk-DenemeImzasi.pfx'
        $cer = Join-Path $staging 'NestDesk-DenemeImzasi.cer'
        try {
            Export-PfxCertificate -Cert $cert -FilePath $pfx -Password (ConvertTo-SecureString $password -AsPlainText -Force) | Out-Null
            Export-Certificate -Cert $cert -FilePath $cer | Out-Null
        } finally {
            # Sertifika deposunda iz kalmasın: imza .pfx dosyasından atılır.
            Remove-Item (Join-Path 'Cert:\CurrentUser\My' $cert.Thumbprint) -DeleteKey
        }
        $signArgs = @('sign', '/fd', 'SHA256', '/f', $pfx, '/p', $password)
    }
    $signtool = Join-Path $tools 'signtool.exe'
    function Sign-Package([string]$file) { if ($signArgs) { Invoke-Tool $signtool ($signArgs + $file) } }

    # --- Görseller ve resources.pri (mimariden bağımsız, bir kez) ---
    Write-Host '==> Görseller ve resources.pri hazırlanıyor…'
    $priRoot = Join-Path $work 'pri'
    New-Assets (Join-Path $priRoot 'Assets')
    # resources.pri varken makeappx bildirimdeki görsellerin gerçekten var olduğunu denetlemiyor (Store'da reddedilir).
    foreach ($ref in [regex]::Matches($template, 'Assets\\[\w-]+\.png') | ForEach-Object Value | Sort-Object -Unique) {
        $base = [IO.Path]::GetFileNameWithoutExtension($ref)
        if (-not (Get-ChildItem (Join-Path $priRoot 'Assets') -Filter "$base.*png")) { throw "Bildirimdeki $ref için görsel üretilmedi." }
    }
    # makepri yalnızca kimlik adını (kaynak haritası adı) okur; mimari önemsiz.
    $priManifest = Join-Path $work 'pri-manifest\AppxManifest.xml'
    Write-Manifest $Arch[0] $priManifest
    $priConfig = Join-Path $work 'priconfig.xml'
    Invoke-Tool $makepri @('createconfig', '/cf', $priConfig, '/dq', 'tr-TR', '/pv', '10.0.0', '/o')
    # <packaging> bölümü PRI'yi dil/ölçek başına ayrı dosyalara böler (kaynak paketleri içindir); tek resources.pri istiyoruz.
    $config = New-Object xml
    $config.Load($priConfig)
    $packaging = $config.DocumentElement.SelectSingleNode('packaging')
    if ($packaging) { [void]$config.DocumentElement.RemoveChild($packaging) }
    $config.Save($priConfig)
    $pri = Join-Path $work 'resources.pri'
    Invoke-Tool $makepri @('new', '/pr', $priRoot, '/cf', $priConfig, '/mn', $priManifest, '/of', $pri, '/o')

    # --- Her mimari için yayın ve .msix ---
    $packages = Join-Path $work 'msix'
    New-Item -ItemType Directory -Force $packages | Out-Null
    # Ara dosyalar src\...\obj yerine geçici klasöre: aynı anda çalışan başka bir derlemeyle çakışmaz.
    $artifacts = Join-Path $work 'artifacts'
    foreach ($a in $Arch) {
        $layout = Join-Path $work "layout-$a"
        Write-Host "==> $a yayınlanıyor…"
        dotnet publish $project -c Release -r "win-$a" --self-contained true `
            -p:PublishSingleFile=false -p:PublishReadyToRun=true -p:DebugType=none -p:GenerateDocumentationFile=false `
            --artifacts-path $artifacts --disable-build-servers -o $layout -nologo -v q
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish başarısız: $a" }
        foreach ($reserved in 'AppxManifest.xml', 'resources.pri', 'Assets') {
            if (Test-Path (Join-Path $layout $reserved)) { throw "Yayın çıktısında paket dosyasıyla çakışan öğe var: $reserved" }
        }
        Copy-Item (Join-Path $priRoot 'Assets') (Join-Path $layout 'Assets') -Recurse
        Copy-Item $pri $layout
        Write-Manifest $a (Join-Path $layout 'AppxManifest.xml')

        Write-Host "==> $a paketleniyor…"
        $msix = Join-Path $packages "NestDesk_${packageVersion}_$a.msix"
        Invoke-Tool $makeappx @('pack', '/d', $layout, '/p', $msix, '/o')
        Sign-Package $msix
        Remove-Item $layout -Recurse -Force
    }

    # --- Store'a yüklenecek paket kümesi ---
    Write-Host '==> .msixbundle oluşturuluyor…'
    $bundle = Join-Path $staging "NestDesk-$version.msixbundle"
    Invoke-Tool $makeappx @('bundle', '/d', $packages, '/p', $bundle, '/bv', $packageVersion, '/o')
    Sign-Package $bundle

    # --- Çıktıyı yerine koy: eski içerik ancak şimdi silinir ---
    New-Item -ItemType Directory -Force $outDir | Out-Null
    try { Get-ChildItem $outDir -Force | Remove-Item -Recurse -Force }
    catch { throw "$outDir içindeki eski dosyalar silinemedi (biri açık olabilir); yeni paket kaydedilmedi, kapatıp yeniden çalıştır. $($_.Exception.Message)" }
    Get-ChildItem $staging | Move-Item -Destination $outDir
    $bundle = Join-Path $outDir (Split-Path -Leaf $bundle)
    if ($TestSign) { $pfx = Join-Path $outDir (Split-Path -Leaf $pfx); $cer = Join-Path $outDir (Split-Path -Leaf $cer) }
} finally {
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ''
Write-Host ('{0}  ({1:N1} MB; {2}; sürüm {3})' -f $bundle, ((Get-Item $bundle).Length / 1MB), ($Arch -join ', '), $packageVersion)
if ($TestSign) {
    Write-Host "Deneme imzası: $pfx (parola: $password)"
    Write-Host "Kurmadan önce (yönetici): Import-Certificate -FilePath `"$cer`" -CertStoreLocation Cert:\LocalMachine\TrustedPeople"
}
if ($isPlaceholder) { Write-Warning $placeholderMessage }
