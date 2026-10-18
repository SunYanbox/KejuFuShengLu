param([string]$Tab = '成就', [int]$Months = 6)

$exe = (Resolve-Path 'src\KFL.App\bin\Debug\net10.0-windows\KFL.App.exe').Path
Get-Process -Name KFL.App -ErrorAction SilentlyContinue | ForEach-Object { $_.Kill() }
$env:KFL_SMOKE = $null
$env:KFL_AUTOSTART = "$Months"
$err = Join-Path (Get-Location) 'probe-err.txt'
Remove-Item $err -ErrorAction SilentlyContinue
$proc = Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe) -PassThru -RedirectStandardError $err

$hwnd = [IntPtr]::Zero
for ($i = 0; $i -lt 40 -and $hwnd -eq [IntPtr]::Zero; $i++) {
    Start-Sleep -Milliseconds 500
    $proc.Refresh()
    if ($proc.HasExited) { "app exited early: $($proc.ExitCode)"; break }
    $hwnd = $proc.MainWindowHandle
}

if (-not $proc.HasExited) {
    Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $Tab)
    $el = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
    "found element: $($null -ne $el)"
    try {
        $pattern = $el.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
        $pattern.Select()
        Start-Sleep -Seconds 2
        $proc.Refresh()
        "after select exited: $($proc.HasExited)"
    }
    catch {
        "select threw: $($_.Exception.Message)"
    }

    if (-not $proc.HasExited) { $proc.Kill() }
}

Start-Sleep -Seconds 1
"--- stderr"; Get-Content $err -ErrorAction SilentlyContinue | Select-Object -First 25
