# Render native WinForms dialog in both themes for visual QA (Windows CI).
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$exe = (Resolve-Path (Join-Path $repo 'FastDownloader.exe')).Path
$asm = [Reflection.Assembly]::LoadFile($exe)
$theme = $asm.GetType('FastDownloader.NativeDialogTheme')
$pendingType = $asm.GetType('FastDownloader.PendingDownloadInfo')
$dialogType = $asm.GetType('FastDownloader.DownloadConfirmDialog')
if (-not $theme -or -not $pendingType -or -not $dialogType) { throw 'Native dialog types missing' }
$save = $theme.GetMethod('Save')
$ctor = $dialogType.GetConstructor([type[]]@($pendingType, [string]))
$out = Join-Path $repo 'dialog-preview'
New-Item -ItemType Directory -Path $out -Force | Out-Null
foreach ($mode in @('dark','light','zaxcore')) {
    $save.Invoke($null, [object[]]@([string]$mode)) | Out-Null
    $p = [Activator]::CreateInstance($pendingType)
    $pendingType.GetProperty('filename').SetValue($p, 'Never Leak API Keys: Try This 80ms Tool - YouTube.mp4', $null)
    $pendingType.GetProperty('title').SetValue($p, 'Never Leak API Keys: Try This 80ms Tool - YouTube', $null)
    $pendingType.GetProperty('url').SetValue($p, 'https://youtube.com/watch?v=sample', $null)
    $pendingType.GetProperty('category').SetValue($p, 'Video', $null)
    $form = $null
    $bitmap = $null
    try {
        $form = [System.Windows.Forms.Form]$ctor.Invoke([object[]]@($p, [string]$env:TEMP))
        # DrawToBitmap on an unshown Form captures only its solid background.
        # Create real child-control handles and pump Windows messages first.
        $form.Location = New-Object System.Drawing.Point(-1600, -1600)
        $form.Show()
        [System.Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 250
        $form.Refresh()
        [System.Windows.Forms.Application]::DoEvents()
        $bitmap = New-Object System.Drawing.Bitmap($form.Width, $form.Height)
        $area = New-Object System.Drawing.Rectangle(0,0,$form.Width,$form.Height)
        $form.DrawToBitmap($bitmap, $area)
        # Check that header, text fields and buttons were actually painted.
        $colorCount = New-Object 'System.Collections.Generic.HashSet[int]'
        for ($y = 20; $y -lt $bitmap.Height; $y += 12) {
            for ($x = 20; $x -lt $bitmap.Width; $x += 12) {
                [void]$colorCount.Add($bitmap.GetPixel($x,$y).ToArgb())
            }
        }
        if ($colorCount.Count -lt 8) {
            throw "Preview $mode is blank ($($colorCount.Count) sampled colors): visual QA rejected."
        }
        $path = Join-Path $out ('dialog-' + $mode + '.png')
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        if ((Get-Item $path).Length -lt 1500) { throw "Dialog preview was unexpectedly small: $mode" }
        Write-Host "Rendered $path ($($form.Width)x$($form.Height))"
    } finally {
        if ($bitmap) { $bitmap.Dispose() }
        if ($form) { $form.Dispose() }
    }
}
$save.Invoke($null, [object[]]@('dark')) | Out-Null
