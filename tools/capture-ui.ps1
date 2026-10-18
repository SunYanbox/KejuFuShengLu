Add-Type -AssemblyName System.Drawing

$sig = @'
using System;
using System.Runtime.InteropServices;
public static class Win32Cap {
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int cmd);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
'@
Add-Type -TypeDefinition $sig

$exe = (Resolve-Path 'src\KFL.App\bin\Debug\net10.0-windows\KFL.App.exe').Path
Get-Process -Name KFL.App -ErrorAction SilentlyContinue | ForEach-Object { $_.Kill() }
$env:KFL_SMOKE = $null
$env:KFL_AUTOSTART = if ($args[1]) { $args[1] } else { $null }
$proc = Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe) -PassThru `
    -RedirectStandardOutput 'artifacts\capture-app-out.txt' -RedirectStandardError 'artifacts\capture-app-err.txt'
$hwnd = [IntPtr]::Zero
for ($i = 0; $i -lt 40 -and $hwnd -eq [IntPtr]::Zero; $i++) {
    Start-Sleep -Milliseconds 500
    $proc.Refresh()
    $hwnd = $proc.MainWindowHandle
}
if ($hwnd -eq [IntPtr]::Zero) { "NO WINDOW"; $proc.Kill(); exit 1 }

[Win32Cap]::ShowWindow($hwnd, 3) | Out-Null
[Win32Cap]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Seconds 6

if ($args[2]) {
    Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $args[2])
    $el = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
    if ($null -eq $el) {
        "tab '$($args[2])' NOT FOUND"
    }
    else {
        $pattern = $el.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
        if ($null -eq $pattern) { "tab not selectable" } else { $pattern.Select(); "tab selected" }
        Start-Sleep -Seconds 2
    }
}

$rect = New-Object Win32Cap+RECT
[Win32Cap]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
$w = $rect.Right - $rect.Left
$h = $rect.Bottom - $rect.Top
"window ${w}x${h}"

$bmp = New-Object System.Drawing.Bitmap $w, $h
$gfx = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $gfx.GetHdc()
[Win32Cap]::PrintWindow($hwnd, $hdc, 2) | Out-Null
$gfx.ReleaseHdc($hdc)
$gfx.Dispose()

New-Item -ItemType Directory -Force -Path 'artifacts' | Out-Null
$out = Join-Path (Get-Location) "artifacts\ui-$($args[0]).png"
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
$proc.Kill()
"saved $out size=$((Get-Item $out).Length)"
