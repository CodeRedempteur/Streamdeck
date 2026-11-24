# =========================================
#  NfcNative.ps1 - fonctions Windows utilisées par server.ps1
#  (volume, touches multimédia, veille, verrouillage, extinction...)
#  Chargé automatiquement par server.ps1 : pas à lancer soi-même.
# =========================================

$script:NfcIsWindows = [Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT

if ($script:NfcIsWindows -and -not ('NfcAudio' -as [type])) {

    # Code C# volontairement simple (compatible Windows PowerShell 5.1 comme PowerShell 7).
    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

[Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioEndpointVolume {
    int RegisterControlChangeNotify(IntPtr pNotify);
    int UnregisterControlChangeNotify(IntPtr pNotify);
    int GetChannelCount(out uint pnChannelCount);
    int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
    int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
    int GetMasterVolumeLevel(out float pfLevelDB);
    int GetMasterVolumeLevelScalar(out float pfLevel);
    int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
    int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
    int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
    int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
    int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
    int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
}

[Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDevice {
    int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, out IAudioEndpointVolume ppInterface);
}

[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceEnumerator {
    int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);
    int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
}

[ComImport, Guid("BCDE0395-E52F-46C0-9BE0-D9AC5F1C8F35")]
class MMDeviceEnumeratorComObject { }

// Volume et muet de la sortie audio par défaut de Windows.
public static class NfcAudio {
    private static IAudioEndpointVolume Endpoint() {
        IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumeratorComObject());
        IMMDevice device;
        Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(0, 1, out device)); // eRender, eMultimedia
        Guid iid = typeof(IAudioEndpointVolume).GUID;
        IAudioEndpointVolume volume;
        Marshal.ThrowExceptionForHR(device.Activate(ref iid, 23, IntPtr.Zero, out volume)); // CLSCTX_ALL
        return volume;
    }

    public static float GetVolume() {
        float level;
        Marshal.ThrowExceptionForHR(Endpoint().GetMasterVolumeLevelScalar(out level));
        return level;
    }

    public static void SetVolume(float level) {
        Guid none = Guid.Empty;
        Marshal.ThrowExceptionForHR(Endpoint().SetMasterVolumeLevelScalar(level, ref none));
    }

    public static bool GetMute() {
        bool muted;
        Marshal.ThrowExceptionForHR(Endpoint().GetMute(out muted));
        return muted;
    }

    public static void SetMute(bool muted) {
        Guid none = Guid.Empty;
        Marshal.ThrowExceptionForHR(Endpoint().SetMute(muted, ref none));
    }
}


// Seconde implementation COM, ecrite dans la forme classique (Guid par valeur, parametres entiers) :
// utilisee si la premiere echoue, et l'erreur de chacune est remontee au diagnostic.
[Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioEndpointVolumeC {
    int f(); int g(); int h(); int i();
    int SetMasterVolumeLevelScalar(float fLevel, System.Guid pguidEventContext);
    int j();
    int GetMasterVolumeLevelScalar(out float pfLevel);
    int k(); int l(); int m(); int n();
    int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, System.Guid pguidEventContext);
    int GetMute(out bool pbMute);
}

[Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceC {
    int Activate(ref System.Guid id, int clsCtx, int activationParams, out IAudioEndpointVolumeC aev);
}

[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceEnumeratorC {
    int f();
    int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDeviceC endpoint);
}

public static class NfcAudioClassic {
    private static IAudioEndpointVolumeC Vol() {
        IMMDeviceEnumeratorC enumerator = new MMDeviceEnumeratorComObject() as IMMDeviceEnumeratorC;
        IMMDeviceC dev = null;
        Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(0, 1, out dev));
        IAudioEndpointVolumeC epv = null;
        Guid epvid = typeof(IAudioEndpointVolumeC).GUID;
        Marshal.ThrowExceptionForHR(dev.Activate(ref epvid, 23, 0, out epv));
        return epv;
    }

    public static float GetVolume() {
        float v = -1;
        Marshal.ThrowExceptionForHR(Vol().GetMasterVolumeLevelScalar(out v));
        return v;
    }

    public static void SetVolume(float level) {
        Marshal.ThrowExceptionForHR(Vol().SetMasterVolumeLevelScalar(level, Guid.Empty));
    }

    public static bool GetMute() {
        bool m;
        Marshal.ThrowExceptionForHR(Vol().GetMute(out m));
        return m;
    }

    public static void SetMute(bool mute) {
        Marshal.ThrowExceptionForHR(Vol().SetMute(mute, Guid.Empty));
    }
}

// Touches du clavier envoyées à Windows (fonctionne sans fenetre active).
public static class NfcInput {
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private const uint EXTENDED = 0x1;
    private const uint KEYUP = 0x2;

    public static void Tap(byte key) {
        keybd_event(key, 0, EXTENDED, UIntPtr.Zero);
        keybd_event(key, 0, EXTENDED | KEYUP, UIntPtr.Zero);
    }

    public static void Combo(byte modifier, byte key) {
        keybd_event(modifier, 0, 0, UIntPtr.Zero);
        keybd_event(key, 0, 0, UIntPtr.Zero);
        keybd_event(key, 0, KEYUP, UIntPtr.Zero);
        keybd_event(modifier, 0, KEYUP, UIntPtr.Zero);
    }
}

public static class NfcMonitor {
    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    // WM_SYSCOMMAND / SC_MONITORPOWER : 2 = éteindre les écrans.
    public static void Off() {
        PostMessage((IntPtr)0xFFFF, 0x0112, (IntPtr)0xF170, (IntPtr)2);
    }
}
"@ -ErrorAction Stop

    Add-Type -AssemblyName System.Windows.Forms -ErrorAction SilentlyContinue
}

function Assert-NfcWindows {
    if (-not $script:NfcIsWindows) { throw "Cette action n'existe que sous Windows." }
}

# ----- Volume -----
#
# Trois moteurs, essayés dans l'ordre jusqu'à ce que l'un fonctionne :
#   com      API audio de Windows (lecture + réglage exacts)
#   classic  même API, autre écriture du code d'interface
#   keys     touches volume du clavier (comme les touches média) : marche partout,
#            mais le niveau affiché est alors une ESTIMATION (Windows ne peut pas être lu par les touches)
# Chaque échec est mémorisé et renvoyé dans "warning" pour qu'on sache pourquoi.

$script:AudioEngine = $null
$script:AudioErrors = @{}
$script:EstLevel = 50
$script:EstMuted = $false

# Appels de bas niveau (séparés pour pouvoir être remplacés dans les tests).
function Read-NfcAudioCom     { @{ level = [int][Math]::Round([NfcAudio]::GetVolume() * 100); muted = [bool][NfcAudio]::GetMute() } }
function Read-NfcAudioClassic { @{ level = [int][Math]::Round([NfcAudioClassic]::GetVolume() * 100); muted = [bool][NfcAudioClassic]::GetMute() } }
function Write-NfcVolumeCom     { param([int]$Level) [NfcAudio]::SetVolume([single]($Level / 100.0)) }
function Write-NfcVolumeClassic { param([int]$Level) [NfcAudioClassic]::SetVolume([single]($Level / 100.0)) }
function Write-NfcMuteCom       { param([bool]$Muted) [NfcAudio]::SetMute($Muted) }
function Write-NfcMuteClassic   { param([bool]$Muted) [NfcAudioClassic]::SetMute($Muted) }

function Send-NfcVolumeKey {
    param([Parameter(Mandatory = $true)][ValidateSet('up', 'down', 'mute')][string]$Key)
    $codes = @{ up = 0xAF; down = 0xAE; mute = 0xAD }
    [NfcInput]::Tap([byte]$codes[$Key])
}

function Get-NfcErrorText {
    param($ErrorRecord)
    $ex = $ErrorRecord.Exception
    $base = $ex.GetBaseException()
    return ("{0} : {1} (0x{2:X8})" -f $base.GetType().Name, $base.Message.Trim(), $base.HResult)
}

# Essaie chaque moteur ; renvoie @{ ok; value }. Mémorise le moteur qui marche et l'erreur de ceux qui échouent.
function Invoke-NfcAudioEngines {
    param([scriptblock]$Com, [scriptblock]$Classic, $Argument = $null)

    $order = if ($script:AudioEngine -eq 'classic') { @('classic', 'com') } else { @('com', 'classic') }

    foreach ($engine in $order) {
        try {
            $value = if ($engine -eq 'com') { & $Com $Argument } else { & $Classic $Argument }
            $script:AudioEngine = $engine
            $script:AudioErrors.Remove($engine)
            return @{ ok = $true; value = $value }
        }
        catch {
            $script:AudioErrors[$engine] = Get-NfcErrorText $_
        }
    }
    return @{ ok = $false; value = $null }
}

function Get-NfcAudioWarning {
    if ($script:AudioErrors.Count -eq 0) { return $null }
    return (($script:AudioErrors.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Name): $($_.Value)" }) -join " | ")
}

# Renvoie @{ level = 0..100; muted; engine; estimated (si repli clavier); warning (si un moteur a échoué) }
function Get-NfcAudio {
    Assert-NfcWindows

    $r = Invoke-NfcAudioEngines -Com { Read-NfcAudioCom } -Classic { Read-NfcAudioClassic }
    if ($r.ok) {
        $script:EstLevel = [int]$r.value.level
        $script:EstMuted = [bool]$r.value.muted
        return @{ level = $script:EstLevel; muted = $script:EstMuted; engine = $script:AudioEngine }
    }

    return @{ level = $script:EstLevel; muted = $script:EstMuted; engine = 'keys'; estimated = $true; warning = (Get-NfcAudioWarning) }
}

function Set-NfcVolume {
    param([Parameter(Mandatory = $true)][int]$Level)
    Assert-NfcWindows

    $clamped = [Math]::Max(0, [Math]::Min(100, $Level))
    $r = Invoke-NfcAudioEngines -Com { param($l) Write-NfcVolumeCom -Level $l } -Classic { param($l) Write-NfcVolumeClassic -Level $l } -Argument $clamped
    if ($r.ok) { $script:EstLevel = $clamped; return }

    # Repli clavier : 50 x "moins" (0 %), puis autant de "plus" que nécessaire (1 appui = 2 %).
    for ($i = 0; $i -lt 50; $i++) { Send-NfcVolumeKey -Key down }
    for ($i = 0; $i -lt [Math]::Round($clamped / 2.0); $i++) { Send-NfcVolumeKey -Key up }
    $script:EstLevel = $clamped
}

# Monte ou baisse le volume d'un pas relatif (ex : +5 / -5).
function Step-NfcVolume {
    param([Parameter(Mandatory = $true)][int]$Delta)
    Assert-NfcWindows

    $state = Get-NfcAudio
    if (-not $state.estimated) {
        Set-NfcVolume -Level ($state.level + $Delta)
        return
    }

    $presses = [Math]::Max(1, [int][Math]::Ceiling([Math]::Abs($Delta) / 2.0))   # 1 appui = 2 %
    $key = if ($Delta -ge 0) { 'up' } else { 'down' }
    for ($i = 0; $i -lt $presses; $i++) { Send-NfcVolumeKey -Key $key }
    $script:EstLevel = [Math]::Max(0, [Math]::Min(100, $script:EstLevel + $(if ($Delta -ge 0) { $presses * 2 } else { -$presses * 2 })))
}

function Set-NfcMute {
    param([Parameter(Mandatory = $true)][bool]$Muted)
    Assert-NfcWindows

    $r = Invoke-NfcAudioEngines -Com { param($m) Write-NfcMuteCom -Muted $m } -Classic { param($m) Write-NfcMuteClassic -Muted $m } -Argument $Muted
    if ($r.ok) { $script:EstMuted = $Muted; return }

    # Repli clavier : la touche « muet » bascule ; on ne l'envoie que si l'état estimé diffère.
    if ($Muted -ne $script:EstMuted) {
        Send-NfcVolumeKey -Key mute
        $script:EstMuted = $Muted
    }
}

# ----- Média (agit sur le lecteur actif : Spotify, navigateur, VLC...) -----

function Send-NfcMediaKey {
    param([Parameter(Mandatory = $true)][ValidateSet('playpause', 'next', 'prev', 'stop')][string]$Key)
    Assert-NfcWindows

    $codes = @{ playpause = 0xB3; next = 0xB0; prev = 0xB1; stop = 0xB2 }
    [NfcInput]::Tap([byte]$codes[$Key])
}

# ----- Bureau -----

function Invoke-NfcDesktop {
    param([Parameter(Mandatory = $true)][ValidateSet('show', 'taskview')][string]$Command)
    Assert-NfcWindows

    $win = [byte]0x5B
    if ($Command -eq 'show') { [NfcInput]::Combo($win, [byte]0x44) }      # Win + D
    else                     { [NfcInput]::Combo($win, [byte]0x09) }      # Win + Tab
}

# ----- Alimentation -----

# Arrêt / redémarrage / annulation : shutdown.exe rend la main tout de suite (le délai laisse le temps de répondre).
function Invoke-NfcShutdown {
    param(
        [Parameter(Mandatory = $true)][ValidateSet('shutdown', 'restart', 'abort')][string]$Mode,
        [int]$DelaySeconds = 5
    )
    Assert-NfcWindows

    $delay = [Math]::Max(0, [Math]::Min(3600, $DelaySeconds))
    switch ($Mode) {
        'shutdown' { & shutdown.exe /s /t $delay /c "Stream Deck" | Out-Null }
        'restart'  { & shutdown.exe /r /t $delay /c "Stream Deck" | Out-Null }
        'abort'    { & shutdown.exe /a | Out-Null }
    }
}

function Invoke-NfcHibernate {
    Assert-NfcWindows
    & shutdown.exe /h | Out-Null
}

# Mise en veille : l'appel ne rend la main qu'au réveil, d'où l'exécution après l'envoi de la réponse.
function Invoke-NfcSleep {
    Assert-NfcWindows
    [System.Windows.Forms.Application]::SetSuspendState([System.Windows.Forms.PowerState]::Suspend, $false, $false) | Out-Null
}

function Invoke-NfcLock {
    Assert-NfcWindows
    Start-Process -FilePath "rundll32.exe" -ArgumentList "user32.dll,LockWorkStation"
}

function Invoke-NfcMonitorOff {
    Assert-NfcWindows
    [NfcMonitor]::Off()
}
