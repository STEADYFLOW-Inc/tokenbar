# ClaudeTokenMeter build script (.NET Framework 4.8 in-box compiler, no SDK required)
$ErrorActionPreference = "Stop"
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { throw ".NET Framework compiler not found: $csc" }

$src = @("Program.cs", "Config.cs", "Strings.cs", "UsageReader.cs", "ApiUsageReader.cs", "AppContext.cs", "WidgetForm.cs", "AssemblyInfo.cs")
$refs = @(
    "/r:System.dll",
    "/r:System.Core.dll",
    "/r:System.Drawing.dll",
    "/r:System.Windows.Forms.dll",
    "/r:System.Web.Extensions.dll"
)

& $csc /nologo /target:winexe /platform:x64 /optimize+ /win32icon:app.ico /out:ClaudeTokenMeter.exe @refs @src
if ($LASTEXITCODE -eq 0) { Write-Host "Build OK: ClaudeTokenMeter.exe" } else { throw "Build failed ($LASTEXITCODE)" }
