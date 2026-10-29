param(
    [switch]$RunTests
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$OutDir = Join-Path $ProjectRoot "bin\Release"
if (-not (Test-Path $OutDir)) {
    New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
}

$CscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$WpfDir = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"
$OutExe = Join-Path $OutDir "StickyTickets.exe"
$ManifestPath = Join-Path $ProjectRoot "app.manifest"
$AppIco = Join-Path $ProjectRoot "assets\app.ico"
$LogoPng = Join-Path $ProjectRoot "assets\logo.png"

$SourceFiles = @(
    (Join-Path $ProjectRoot "src\AppEntry.cs"),
    (Join-Path $ProjectRoot "src\Interop\Win32Interop.cs"),
    (Join-Path $ProjectRoot "src\Data\Models.cs"),
    (Join-Path $ProjectRoot "src\Data\Winsqlite3Native.cs"),
    (Join-Path $ProjectRoot "src\Data\WicImageSafety.cs"),
    (Join-Path $ProjectRoot "src\Data\TicketRepository.cs"),
    (Join-Path $ProjectRoot "src\Data\TicketRepository.Queries.cs"),
    (Join-Path $ProjectRoot "src\Data\TicketRepository.EconomyAndBackup.cs"),
    (Join-Path $ProjectRoot "src\Themes\SkinThemeManager.cs"),
    (Join-Path $ProjectRoot "src\Services\AudioAndUpdateService.cs"),
    (Join-Path $ProjectRoot "src\Services\LocalAiAndOcrService.cs"),
    (Join-Path $ProjectRoot "src\UI\RecipeManagerWindow.cs"),
    (Join-Path $ProjectRoot "src\UI\OnboardingNoticeWindow.cs"),
    (Join-Path $ProjectRoot "src\UI\DeadlineReminderWindow.cs"),
    (Join-Path $ProjectRoot "src\UI\DailyReportAndWishDialog.cs"),
    (Join-Path $ProjectRoot "src\UI\StickyTicketWindow.cs"),
    (Join-Path $ProjectRoot "src\UI\StickyTicketWindow.Visuals.cs"),
    (Join-Path $ProjectRoot "src\UI\StickyTicketWindow.Actions.cs"),
    (Join-Path $ProjectRoot "src\UI\TicketRailWindow.cs"),
    (Join-Path $ProjectRoot "tests\SelfTestRunner.cs")
)

Write-Host "Building StickyTickets.exe with native Windows WPF compiler..."
if (Test-Path $OutExe) {
    $OldExe = Join-Path $OutDir "StickyTickets.old.exe"
    Remove-Item $OldExe -Force -ErrorAction SilentlyContinue
    try {
        Remove-Item $OutExe -Force -ErrorAction Stop
    } catch {
        Move-Item $OutExe $OldExe -Force -ErrorAction SilentlyContinue
    }
}
& $CscPath /nologo /target:winexe /optimize+ /platform:x64 `
    /win32manifest:$ManifestPath `
    /win32icon:$AppIco `
    /resource:"$LogoPng,StickyTickets.logo.png" `
    /out:$OutExe `
    /r:"$WpfDir\PresentationFramework.dll" `
    /r:"$WpfDir\PresentationCore.dll" `
    /r:"$WpfDir\WindowsBase.dll" `
    /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll" `
    $SourceFiles

if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE"
}

Write-Host "Build succeeded: $OutExe"
$CnName = (-join ([char[]]@(0x4EFB, 0x52A1, 0x4FBF, 0x7B7E))) + ".exe"
$CnExe = Join-Path $OutDir $CnName
$RootCnExe = Join-Path $ProjectRoot $CnName
foreach ($TargetExe in @($CnExe, $RootCnExe)) {
    if (Test-Path $TargetExe) {
        $OldTarget = "$TargetExe.old"
        Remove-Item $OldTarget -Force -ErrorAction SilentlyContinue
        try {
            Remove-Item $TargetExe -Force -ErrorAction Stop
        } catch {
            Move-Item $TargetExe $OldTarget -Force -ErrorAction SilentlyContinue
        }
    }
    Copy-Item $OutExe $TargetExe -Force -ErrorAction SilentlyContinue
}

if ($RunTests) {
    Write-Host "Running automated security & architecture self-tests..."
    $proc = Start-Process -FilePath $OutExe -ArgumentList "--self-test" -Wait -PassThru -NoNewWindow
    if ($proc.ExitCode -ne 0) {
        throw "Self-tests failed with exit code $($proc.ExitCode)"
    }
}
