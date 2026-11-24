namespace StreamDeck.ScriptBuilder.Generation;

/// <summary>
/// Contenu de BuilderUtils.ps1 : les fonctions appelées par les scripts générés. L'application l'écrit dans le dossier du
/// serveur NFC (à côté de server.ps1). Il est autonome : il ne dépend pas de WindowUtils.ps1.
/// </summary>
public static class BuilderHelpers
{
    public const int Version = 3;
    public const string FileName = "BuilderUtils.ps1";

    private static readonly string Template = """"""
# =====================================================================
#  BuilderUtils.ps1 - fonctions utilisees par les scripts crees avec Stream Deck Script Builder
#  Fichier gere par l'application : il est reecrit automatiquement lors d'une mise a jour.
# =====================================================================
$SdbUtilsVersion = __VERSION__

Add-Type -AssemblyName System.Windows.Forms -ErrorAction SilentlyContinue

if (-not ('SdbWin' -as [type])) {
    Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

public class SdbWin {
    public delegate bool EnumProc(IntPtr h, IntPtr l);

    [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr h, uint msg, IntPtr wParam, IntPtr lParam);

    private const uint WM_CLOSE = 0x0010;

    public static uint GetPid(IntPtr h) {
        uint pid;
        GetWindowThreadProcessId(h, out pid);
        return pid;
    }

    // Demande a la fenetre de se fermer (comme un clic sur sa croix) : le programme peut refuser ou demander une confirmation.
    public static void RequestClose(IntPtr h) {
        PostMessage(h, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
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

    // Fenetres visibles d'un processus dont le TITRE contient un texte donne (recherche sans tenir compte de la casse).
    // Sert a cibler une page precise d'un navigateur : Chrome/Edge n'exposent pas l'adresse via les fenetres Windows,
    // seulement le titre de l'onglet (affiche dans la barre de titre de sa fenetre).
    public static List<IntPtr> WindowsByTitle(string processName, string titleContains) {
        List<IntPtr> found = new List<IntPtr>();

        EnumWindows(delegate(IntPtr h, IntPtr l) {
            if (!IsWindowVisible(h)) return true;

            int len = GetWindowTextLength(h);
            if (len == 0) return true;

            StringBuilder title = new StringBuilder(len + 1);
            GetWindowText(h, title, title.Capacity);
            if (title.ToString().IndexOf(titleContains, StringComparison.OrdinalIgnoreCase) < 0) return true;

            if (!String.IsNullOrEmpty(processName)) {
                try {
                    uint pid;
                    GetWindowThreadProcessId(h, out pid);
                    using (Process p = Process.GetProcessById((int)pid)) {
                        if (!String.Equals(p.ProcessName, processName, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                } catch (Exception) { return true; }
            }

            found.Add(h);
            return true;
        }, IntPtr.Zero);

        return found;
    }
}

"@ -ErrorAction SilentlyContinue
}

# Attend la fenetre principale d'un processus. Renvoie le processus, ou $null si elle n'apparait pas.
#  - un processus lance apres $StartedAfter est prefere ;
#  - si l'application tournait deja (aucun nouveau processus), une fenetre existante est acceptee
#    apres $GraceSeconds ; avec -AnyWindow, elle est acceptee tout de suite.
function Find-ProcessWindow {
    param(
        [Parameter(Mandatory=$true)][string]$ProcessName,
        [int]$TimeoutSeconds = 30,
        [datetime]$StartedAfter = (Get-Date).AddSeconds(-1),
        [switch]$AnyWindow,
        [int]$GraceSeconds = 4
    )

    $started = Get-Date
    $deadline = $started.AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        $candidates = @(Get-Process -Name $ProcessName -ErrorAction SilentlyContinue |
            Where-Object { $_.MainWindowHandle -ne 0 })

        if (-not $AnyWindow) {
            $new = $candidates | Where-Object { $_.StartTime -ge $StartedAfter } |
                Sort-Object StartTime -Descending | Select-Object -First 1
            if ($new) { return $new }
        }

        if ($candidates.Count -gt 0 -and ($AnyWindow -or ((Get-Date) - $started).TotalSeconds -ge $GraceSeconds)) {
            return ($candidates | Sort-Object StartTime -Descending | Select-Object -First 1)
        }

        Start-Sleep -Milliseconds 500
    }

    Write-Host "  Timeout : fenetre '$ProcessName' non trouvee apres $TimeoutSeconds s"
    return $null
}

# Envoie F11 a une fenetre (plein ecran du navigateur, des lecteurs video, etc.).
function Send-FullscreenKey {
    param([Parameter(Mandatory=$true)][IntPtr]$Handle)

    $ownerPid = [SdbWin]::GetPid($Handle)
    $shell = New-Object -ComObject WScript.Shell
    $shell.AppActivate([int]$ownerPid) | Out-Null
    Start-Sleep -Milliseconds 300
    $shell.SendKeys("{F11}")
}

# Place, met en forme et amene devant une fenetre.
#  -Process : un processus (Find-ProcessWindow) ou une poignee de fenetre
#  -Screen  : -2 = ne pas deplacer, -1 = ecran le plus a droite, 0 = premier ecran (de gauche a droite), 1, 2...
#  -State   : Keep | Maximize | Fullscreen (maximise puis F11) | Minimize | Restore
function Set-ManagedWindow {
    param(
        [Parameter(Mandatory=$true)]$Process,
        [int]$Screen = -2,
        [ValidateSet('Keep','Maximize','Fullscreen','Minimize','Restore')][string]$State = 'Keep',
        [switch]$Foreground
    )

    if (-not $Process) { return }

    $h = if ($Process -is [IntPtr]) { $Process } else { $Process.MainWindowHandle }
    if ([IntPtr]::Zero -eq $h) { return }

    if ($Screen -ne -2) {
        $screens = [System.Windows.Forms.Screen]::AllScreens | Sort-Object { $_.Bounds.X }
        $target = if ($Screen -ge 0 -and $Screen -lt $screens.Count) { $screens[$Screen] } else { $screens[$screens.Count - 1] }
        $b = $target.Bounds

        # Une fenetre maximisee ou reduite ne se deplace pas : on la restaure d'abord (9 = SW_RESTORE).
        [SdbWin]::ShowWindowAsync($h, 9) | Out-Null
        Start-Sleep -Milliseconds 200

        # 0x0040 = SWP_SHOWWINDOW
        [SdbWin]::SetWindowPos($h, [IntPtr]::Zero, $b.X, $b.Y, $b.Width, $b.Height, 0x0040) | Out-Null
    }

    switch ($State) {
        'Maximize'   { [SdbWin]::ShowWindowAsync($h, 3) | Out-Null }
        'Fullscreen' { [SdbWin]::ShowWindowAsync($h, 3) | Out-Null }
        'Minimize'   { [SdbWin]::ShowWindowAsync($h, 6) | Out-Null }
        'Restore'    { [SdbWin]::ShowWindowAsync($h, 9) | Out-Null }
    }

    if (($Foreground -or $State -eq 'Fullscreen') -and $State -ne 'Minimize') {
        if ([SdbWin]::IsIconic($h)) { [SdbWin]::ShowWindowAsync($h, 9) | Out-Null }
        [SdbWin]::BringWindowToTop($h) | Out-Null
        [SdbWin]::SetForegroundWindow($h) | Out-Null
    }

    if ($State -eq 'Fullscreen') {
        Start-Sleep -Milliseconds 800
        Send-FullscreenKey -Handle $h
    }
}

# Ouvre une URL dans une NOUVELLE fenetre du navigateur (meme s'il est deja ouvert), repere cette fenetre
# et la place. Renvoie sa poignee, ou $null.
function Open-BrowserPage {
    param(
        [Parameter(Mandatory=$true)][string]$Url,
        [string]$Browser = 'chrome',
        [int]$Screen = -2,
        [ValidateSet('Keep','Maximize','Fullscreen','Minimize','Restore')][string]$State = 'Keep',
        [int]$TimeoutSeconds = 20
    )

    $class = 'Chrome_WidgetWin_1'
    $before = @([SdbWin]::Windows($Browser, $class))

    Start-Process -FilePath "$Browser.exe" -ArgumentList "--new-window `"$Url`""

    $handle = $null
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline -and -not $handle) {
        $handle = @([SdbWin]::Windows($Browser, $class)) |
            Where-Object { $before -notcontains $_ } | Select-Object -First 1
        if (-not $handle) { Start-Sleep -Milliseconds 300 }
    }

    if (-not $handle) {
        Write-Host "  Timeout : nouvelle fenetre '$Browser' non trouvee apres $TimeoutSeconds s"
        return $null
    }

    Set-ManagedWindow -Process $handle -Screen $Screen -State $State -Foreground
    return $handle
}

# Ferme un programme par son nom de processus (-Force : arret immediat, sinon fermeture normale de la fenetre).
function Stop-ProcessByName {
    param(
        [Parameter(Mandatory=$true)][string]$Name,
        [switch]$Force
    )

    $found = @(Get-Process -Name $Name -ErrorAction SilentlyContinue)
    if ($found.Count -eq 0) {
        Write-Host "  '$Name' n'est pas lance"
        return
    }

    foreach ($p in $found) {
        if ($Force) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
        else { [void]$p.CloseMainWindow() }
    }
}

# Ferme la ou les fenetres d'un navigateur dont le TITRE contient le texte donne (pas l'adresse : Chrome/Edge ne
# l'exposent pas via les fenetres Windows, seulement le titre de la page). Marche pour une page ouverte par
# Open-BrowserPage (nouvelle fenetre dediee) ; un onglet ouvert dans une fenetre existante n'a pas sa propre fenetre.
function Close-BrowserWindow {
    param(
        [string]$Browser = 'chrome',
        [Parameter(Mandatory=$true)][string]$TitleContains,
        [switch]$Force
    )

    $handles = [SdbWin]::WindowsByTitle($Browser, $TitleContains)
    if ($handles.Count -eq 0) {
        Write-Host "  Aucune fenetre de '$Browser' avec un titre contenant '$TitleContains'"
        return
    }

    foreach ($h in $handles) {
        if ($Force) {
            $ownerPid = [SdbWin]::GetPid($h)
            Stop-Process -Id $ownerPid -Force -ErrorAction SilentlyContinue
        } else {
            [SdbWin]::RequestClose($h)
        }
    }
}

# Envoie une commande au petit serveur local du telephone (voir Reglages -> Telephone dans l'app telephone et ici).
#  -Action : 'find' (sonne/vibre le telephone) ou 'openapp' (ouvre une application, avec -AppPackage)
function Send-PhoneCommand {
    param(
        [Parameter(Mandatory=$true)][string]$IpAddress,
        [Parameter(Mandatory=$true)][int]$Port,
        [Parameter(Mandatory=$true)][string]$Token,
        [Parameter(Mandatory=$true)][ValidateSet('find','openapp')][string]$Action,
        [string]$AppPackage = ""
    )

    $url = "http://" + $IpAddress + ":" + $Port + "/command?token=" + [uri]::EscapeDataString($Token) + "&action=" + [uri]::EscapeDataString($Action)
    if ($AppPackage) { $url += "&app=" + [uri]::EscapeDataString($AppPackage) }

    try {
        Invoke-RestMethod -Uri $url -Method Get -TimeoutSec 10 | Out-Null
    } catch {
        Write-Host "  Telephone injoignable ou jeton refuse : $($_.Exception.Message)"
    }
}
"""""";

    /// <summary>Texte de BuilderUtils.ps1 (avec sa version).</summary>
    public static string Content => Template.Replace("__VERSION__", Version.ToString());
}

/// <summary>
/// Contenu de StartServerHidden.vbs, écrit par l'application à côté de server.ps1 (voir <see cref="ScriptStore.EnsureHelpers"/>).
/// Un raccourci vers ce fichier dans le dossier Démarrage de Windows (Win+R, <c>shell:startup</c>) lance le serveur
/// tout seul à l'ouverture de session, sans jamais afficher de fenêtre (même pas un flash de console au démarrage,
/// contrairement à <c>powershell -WindowStyle Hidden</c> lancé directement) — il tourne en tâche de fond du début à
/// la fin, sans apparaître comme une application dans la barre des tâches ou l'alt-tab.
/// </summary>
public static class HiddenLauncher
{
    public const string FileName = "StartServerHidden.vbs";

    private static readonly string Template = """"""
' Lance server.ps1 completement invisible (aucune fenetre, pas meme un flash de console).
' A utiliser via un raccourci dans le dossier Demarrage de Windows (Win+R puis "shell:startup") pour que le
' serveur soit deja lance a l'ouverture de session - il ne s'affiche jamais comme une application.
' Genere par Stream Deck Script Builder : recree automatiquement, ne pas modifier a la main.
Set fso = CreateObject("Scripting.FileSystemObject")
scriptDir = fso.GetParentFolderName(WScript.ScriptFullName)
serverPath = scriptDir & "\server.ps1"
q = Chr(34)
cmd = q & "__SHELL__" & q & " -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File " & q & serverPath & q
Set shell = CreateObject("WScript.Shell")
shell.Run cmd, 0, False
"""""";

    /// <summary>Texte du fichier, avec le programme PowerShell choisi dans les réglages (pwsh.exe ou powershell.exe).</summary>
    public static string Content(string powerShellPath) =>
        Template.Replace("__SHELL__", string.IsNullOrWhiteSpace(powerShellPath) ? "powershell.exe" : powerShellPath.Trim().Trim('"'));
}
