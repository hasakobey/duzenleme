# PostToolUse hook: .cs/.xaml/.csproj degisince dosyanin bulundugu cozumu derler; hata varsa Claude'a bildirir (exit 2).
# Cozum, duzenlenen dosyadan yukari dogru aranir: git worktree'lerinde (paralel ajanlar) her biri kendi kopyasini derler;
# cozum disindaki dosyalar (gecici/karalama kopyalar) derleme tetiklemez.
$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
$file = $payload.tool_input.file_path
if (-not $file -or $file -notmatch '\.(cs|xaml|csproj)$') { exit 0 }

$dir = Split-Path -Parent $file
$sln = $null
while ($dir) {
    $candidate = Join-Path $dir 'Duzenleme.sln'
    if (Test-Path $candidate) { $sln = $candidate; break }
    $parent = Split-Path -Parent $dir
    if ($parent -eq $dir) { break }
    $dir = $parent
}
if (-not $sln) { exit 0 }

$userDotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
if (Test-Path (Join-Path $userDotnet 'dotnet.exe')) { $env:PATH = "$userDotnet;$env:PATH" }
$out = & dotnet build $sln -v q -nologo 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) {
    $errors = ($out -split "`r?`n" | Where-Object { $_ -match ' error ' } | Select-Object -Unique -First 20) -join "`n"
    if (-not $errors) { $errors = $out }
    [Console]::Error.WriteLine("dotnet build FAILED ($sln):`n$errors")
    exit 2
}
exit 0
