$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

# Build names using Unicode code points so PowerShell 5.1 works regardless of BOM
$DistFolderName = -join ([char[]]@(0x6253, 0x5305, 0x53D1, 0x5E03, 0x7248))
$AppExeName = (-join ([char[]]@(0x4EFB, 0x52A1, 0x4FBF, 0x7B7E))) + ".exe"
$RootCleanExeName = (-join ([char[]]@(0x4EFB, 0x52A1, 0x4FBF, 0x7B7E, 0x005F, 0x7EAF, 0x51C0, 0x7248))) + ".exe"
$GuideFileName = (-join ([char[]]@(0x65B0, 0x624B, 0x5FEB, 0x901F, 0x4E0A, 0x624B, 0x6307, 0x5357))) + ".txt"
$ZipFileName = (-join ([char[]]@(0x4EFB, 0x52A1, 0x4FBF, 0x7B7E, 0x005F, 0x7EAF, 0x51C0, 0x6253, 0x5305, 0x7248))) + ".zip"

$ReleaseDir = Join-Path $ProjectRoot $DistFolderName
if (-not (Test-Path $ReleaseDir)) {
    New-Item -ItemType Directory -Force -Path $ReleaseDir | Out-Null
}

$CscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$WpfDir = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"
$CleanExe = Join-Path $ReleaseDir $AppExeName
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

if (Test-Path $CleanExe) {
    Remove-Item $CleanExe -Force
}

Write-Host "Building clean distribution executable (/d:CLEAN_RELEASE)..."
& $CscPath /nologo /target:winexe /optimize+ /platform:x64 /d:CLEAN_RELEASE `
    /win32manifest:$ManifestPath `
    /win32icon:$AppIco `
    /resource:"$LogoPng,StickyTickets.logo.png" `
    /out:$CleanExe `
    /r:"$WpfDir\PresentationFramework.dll" `
    /r:"$WpfDir\PresentationCore.dll" `
    /r:"$WpfDir\WindowsBase.dll" `
    /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll" `
    $SourceFiles

if ($LASTEXITCODE -ne 0) {
    throw "Clean build compilation failed with exit code $LASTEXITCODE"
}

Write-Host "Running automated self-tests on clean build..."
$proc = Start-Process -FilePath $CleanExe -ArgumentList "--self-test" -Wait -PassThru -NoNewWindow
if ($proc.ExitCode -ne 0) {
    throw "Clean build self-tests failed with exit code $($proc.ExitCode)"
}

Copy-Item -Path (Join-Path $ProjectRoot "assets\onboarding_guide.txt") -Destination (Join-Path $ReleaseDir $GuideFileName) -Force
$RootCleanExePath = Join-Path $ProjectRoot $RootCleanExeName
Copy-Item -Path $CleanExe -Destination $RootCleanExePath -Force

$ZipPath = Join-Path $ProjectRoot $ZipFileName
if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($ReleaseDir, $ZipPath)
Write-Host "Clean package ready:"
Write-Host "  Folder: $ReleaseDir"
Write-Host "  Zip:    $ZipPath"
