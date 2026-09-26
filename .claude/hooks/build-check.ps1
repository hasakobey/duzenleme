# PostToolUse hook: .cs/.xaml/.csproj degisince cozumu derler; hata varsa Claude'a bildirir (exit 2).
$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
$file = $payload.tool_input.file_path
if (-not $file -or $file -notmatch '\.(cs|xaml|csproj)$') { exit 0 }

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$out = & dotnet build (Join-Path $root 'Duzenleme.sln') -v q -nologo 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) {
    $errors = ($out -split "`r?`n" | Where-Object { $_ -match ' error ' } | Select-Object -Unique -First 20) -join "`n"
    if (-not $errors) { $errors = $out }
    [Console]::Error.WriteLine("dotnet build FAILED:`n$errors")
    exit 2
}
exit 0
