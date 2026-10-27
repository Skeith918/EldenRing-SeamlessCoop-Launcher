# Compile le launcher en un seul .exe avec le compilateur C# fourni avec Windows (.NET Framework 4.8) : rien à installer.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$fw  = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
$out = Join-Path $PSScriptRoot "EldenRing-SeamlessCoop-Launcher.exe"
if (-not (Test-Path "assets\app.ico")) { & "$PSScriptRoot\tools\make-icon.ps1" | Out-Null }

$refs = @(
    "$fw\WPF\PresentationFramework.dll", "$fw\WPF\PresentationCore.dll", "$fw\WPF\WindowsBase.dll",
    "$fw\System.Xaml.dll", "$fw\System.dll", "$fw\System.Core.dll",
    "$fw\System.IO.Compression.dll", "$fw\System.IO.Compression.FileSystem.dll", "$fw\System.Web.Extensions.dll",
    "$fw\System.Windows.Forms.dll"
) | ForEach-Object { "/r:$_" }

& "$fw\csc.exe" /nologo /target:winexe /optimize+ /platform:anycpu "/out:$out" `
    /win32icon:assets\app.ico /win32manifest:assets\app.manifest `
    /resource:src\MainWindow.xaml,MainWindow.xaml `
    /resource:assets\background.jpg,background.jpg `
    /resource:assets\Cinzel.ttf,Cinzel.ttf `
    /resource:catalog.json,catalog.json `
    $(Get-ChildItem assets\locale\*.json | ForEach-Object { "/resource:assets\locale\$($_.Name),locale.$($_.Name)" }) `
    $refs src\*.cs
if ($LASTEXITCODE -ne 0) { throw "compilation échouée" }
"OK -> $out ({0:N0} Ko)" -f ((Get-Item $out).Length / 1KB)
