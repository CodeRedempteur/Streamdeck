Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class Win32Window {
    [DllImport("user32.dll")]
    public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
}
"@ -ErrorAction SilentlyContinue

Add-Type -AssemblyName System.Windows.Forms -ErrorAction SilentlyContinue

# Attend qu'un processus (lancé apres $StartedAfter) ait une fenetre visible.
function Wait-ForProcessWindow {
    param(
        [Parameter(Mandatory=$true)][string]$ProcessName,
        [int]$TimeoutSeconds = 30,
        [datetime]$StartedAfter = (Get-Date).AddSeconds(-1)
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        $proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue |
            Where-Object {
                $_.MainWindowHandle -ne 0 -and $_.StartTime -ge $StartedAfter
            } |
            Sort-Object StartTime -Descending |
            Select-Object -First 1

        if ($proc) { return $proc }
        Start-Sleep -Milliseconds 500
    }

    Write-Host "  Timeout : fenetre '$ProcessName' non trouvee apres $TimeoutSeconds s"
    return $null
}

# Maximise la fenetre d'un processus, sur l'ecran ou elle se trouve deja.
function Set-WindowMaximized {
    param([Parameter(Mandatory=$true)]$Process)

    if (-not $Process) { return }
    [Win32Window]::ShowWindowAsync($Process.MainWindowHandle, 3) | Out-Null  # 3 = SW_MAXIMIZE
}

# Deplace (et optionnellement maximise) la fenetre d'un processus sur un ecran donne.
# ScreenIndex : -1 = ecran le plus a droite, 0 = premier ecran (souvent le principal), etc.
function Set-WindowOnScreen {
    param(
        [Parameter(Mandatory=$true)]$Process,
        [int]$ScreenIndex = -1,
        [switch]$Maximize
    )

    if (-not $Process) { return }

    $screens = [System.Windows.Forms.Screen]::AllScreens | Sort-Object { $_.Bounds.X }

    if ($ScreenIndex -ge 0 -and $ScreenIndex -lt $screens.Count) {
        $target = $screens[$ScreenIndex]
    }
    else {
        $target = $screens[$screens.Count - 1]   # le plus a droite
    }

    $b = $target.Bounds

    # HWND_TOP = 0, SWP_SHOWWINDOW = 0x0040
    [Win32Window]::SetWindowPos($Process.MainWindowHandle, [IntPtr]::Zero, $b.X, $b.Y, $b.Width, $b.Height, 0x0040) | Out-Null

    if ($Maximize) {
        Set-WindowMaximized -Process $Process
    }
}

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class Win32Foreground {
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool BringWindowToTop(IntPtr hWnd);
}
"@ -ErrorAction SilentlyContinue

function Set-WindowForeground {
    param(
        [Parameter(Mandatory=$true)]$Process
    )

    if (-not $Process) { return }

    $hWnd = $Process.MainWindowHandle

    if ($hWnd -eq 0) { return }

    # 9 = SW_RESTORE
    [Win32Foreground]::ShowWindowAsync($hWnd, 9) | Out-Null

    [Win32Foreground]::BringWindowToTop($hWnd) | Out-Null
    [Win32Foreground]::SetForegroundWindow($hWnd) | Out-Null
}


# =====================================================================
#  Fenetres retrouvees par leur "poignee" (handle)
#  Utile quand l'application est DEJA ouverte : Wait-ForProcessWindow cherche un
#  NOUVEAU processus, donc ne trouve rien pour Chrome s'il tourne deja.
# =====================================================================
Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

public class Win32Enum {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int max);

    public static uint GetPid(IntPtr hWnd) {
        uint pid;
        GetWindowThreadProcessId(hWnd, out pid);
        return pid;
    }

    // Fenetres visibles (avec un titre) d'un processus, filtrees par classe de fenetre si demande.
    public static List<IntPtr> Windows(string processName, string className) {
        List<IntPtr> found = new List<IntPtr>();

        EnumWindows(delegate(IntPtr h, IntPtr l) {
            if (!IsWindowVisible(h) || GetWindowTextLength(h) == 0) return true;

            if (!String.IsNullOrEmpty(className)) {
                StringBuilder sb = new StringBuilder(256);
                GetClassName(h, sb, 256);
                if (sb.ToString() != className) return true;
            }

            try {
                uint pid;
                GetWindowThreadProcessId(h, out pid);
                using (Process p = Process.GetProcessById((int)pid)) {
                    if (String.Equals(p.ProcessName, processName, StringComparison.OrdinalIgnoreCase)) found.Add(h);
                }
            } catch (Exception) { }

            return true;
        }, IntPtr.Zero);

        return found;
    }
}
"@ -ErrorAction SilentlyContinue

# Poignees des fenetres visibles d'un processus (ex : "chrome" + classe "Chrome_WidgetWin_1").
function Get-WindowHandles {
    param(
        [Parameter(Mandatory=$true)][string]$ProcessName,
        [string]$ClassName = ""
    )

    return @([Win32Enum]::Windows($ProcessName, $ClassName))
}

# Attend qu'une fenetre qui n'existait pas avant ($KnownHandles) apparaisse. Renvoie sa poignee, ou $null.
function Wait-ForNewWindow {
    param(
        [Parameter(Mandatory=$true)][string]$ProcessName,
        [string]$ClassName = "",
        [IntPtr[]]$KnownHandles = @(),
        [int]$TimeoutSeconds = 15
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        $new = Get-WindowHandles -ProcessName $ProcessName -ClassName $ClassName |
            Where-Object { $KnownHandles -notcontains $_ } |
            Select-Object -First 1

        if ($new) { return $new }
        Start-Sleep -Milliseconds 300
    }

    Write-Host "  Timeout : nouvelle fenetre '$ProcessName' non trouvee apres $TimeoutSeconds s"
    return $null
}

# Meme chose que Set-WindowOnScreen, mais a partir d'une poignee de fenetre.
# ScreenIndex : -1 = ecran le plus a droite, 0 = premier ecran, etc.
function Set-WindowHandleOnScreen {
    param(
        [Parameter(Mandatory=$true)][IntPtr]$Handle,
        [int]$ScreenIndex = -1,
        [switch]$Maximize
    )

    $screens = [System.Windows.Forms.Screen]::AllScreens | Sort-Object { $_.Bounds.X }

    if ($ScreenIndex -ge 0 -and $ScreenIndex -lt $screens.Count) {
        $target = $screens[$ScreenIndex]
    }
    else {
        $target = $screens[$screens.Count - 1]   # le plus a droite
    }

    $b = $target.Bounds

    # Une fenetre maximisee ne se deplace pas : on la restaure (9) avant de la positionner.
    [Win32Foreground]::ShowWindowAsync($Handle, 9) | Out-Null
    Start-Sleep -Milliseconds 150

    # HWND_TOP = 0, SWP_SHOWWINDOW = 0x0040
    [Win32Window]::SetWindowPos($Handle, [IntPtr]::Zero, $b.X, $b.Y, $b.Width, $b.Height, 0x0040) | Out-Null

    if ($Maximize) {
        [Win32Window]::ShowWindowAsync($Handle, 3) | Out-Null   # 3 = SW_MAXIMIZE
    }
}

function Set-WindowHandleForeground {
    param([Parameter(Mandatory=$true)][IntPtr]$Handle)

    [Win32Foreground]::ShowWindowAsync($Handle, 9) | Out-Null   # 9 = SW_RESTORE
    [Win32Foreground]::BringWindowToTop($Handle) | Out-Null
    [Win32Foreground]::SetForegroundWindow($Handle) | Out-Null
}

# Ouvre une URL dans une NOUVELLE fenetre du navigateur (meme s'il est deja ouvert), la place sur
# l'ecran demande et renvoie sa poignee ($null si elle n'a pas ete trouvee).
function Open-BrowserWindow {
    param(
        [Parameter(Mandatory=$true)][string]$Url,
        [int]$ScreenIndex = -1,
        [switch]$Maximize,
        [string]$Browser = "chrome",
        [int]$TimeoutSeconds = 15
    )

    $before = @(Get-WindowHandles -ProcessName $Browser -ClassName "Chrome_WidgetWin_1")

    Start-Process "$Browser.exe" "--new-window `"$Url`""

    $handle = Wait-ForNewWindow -ProcessName $Browser -ClassName "Chrome_WidgetWin_1" `
        -KnownHandles $before -TimeoutSeconds $TimeoutSeconds

    if (-not $handle) { return $null }

    Set-WindowHandleOnScreen -Handle $handle -ScreenIndex $ScreenIndex -Maximize:$Maximize
    Set-WindowHandleForeground -Handle $handle

    return $handle
}
