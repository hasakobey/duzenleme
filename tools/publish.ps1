# .NET kurulumu gerektirmeyen tek dosyalık Duzenleme.exe üretir -> dist/
$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root 'dist'
dotnet publish (Join-Path $root 'src\Duzenleme\Duzenleme.csproj') -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none -o $dist -nologo -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Get-ChildItem $dist -Filter *.exe | ForEach-Object { "{0}  {1:N1} MB" -f $_.FullName, ($_.Length / 1MB) }
