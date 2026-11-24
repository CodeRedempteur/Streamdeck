using System.Runtime.InteropServices;
using StreamDeck.ScriptBuilder.Models;
using StreamDeck.ScriptBuilder.Services;

namespace StreamDeck.ScriptBuilderApp;

/// <summary>
/// Enregistre souris + clavier via deux « hooks » globaux de bas niveau (WH_KEYBOARD_LL, WH_MOUSE_LL) : l'API
/// standard de Windows pour observer les entrées partout sur le PC, pas seulement dans cette fenêtre.
///
/// <para>
/// <b>IMPORTANT — jamais testé.</b> Aucune machine Windows n'était disponible pendant l'écriture de ce fichier ;
/// il n'a donc jamais tourné une seule fois. Écrit en suivant la documentation officielle de <c>SetWindowsHookEx</c>
/// et l'usage standard de ces hooks en .NET, mais plusieurs points ne peuvent être vérifiés qu'en conditions
/// réelles :
/// </para>
/// <list type="bullet">
/// <item>Les hooks bas niveau exigent une boucle de messages Windows active sur le thread qui les installe. Cette
/// méthode part du principe qu'elle est appelée depuis le thread d'interface de l'application (MAUI/WinUI, qui en a
/// une) — appelée d'ailleurs, les évènements ne seraient jamais reçus.</item>
/// <item>Aucun antivirus/EDR testé : certains bloquent ou avertissent sur l'installation de hooks clavier bas niveau
/// (association fréquente avec les key loggers), même légitimes.</item>
/// <item>La touche d'arrêt n'est jamais « avalée » (elle continue vers la fenêtre au premier plan) : choix
/// délibéré pour éviter le risque de modificateur bloqué (Ctrl/Alt restant « enfoncé ») si l'interception se passe
/// mal, mais jamais vérifié en pratique.</item>
/// </list>
/// </summary>
internal sealed class WindowsMacroRecorder : IMacroRecorder
{
    public bool CanRecord => true;

    // Les délégués doivent rester vivants (champs) tant que le hook est installé : sinon le GC peut les libérer
    // pendant que Windows continue d'appeler l'ancien pointeur, qui devient invalide.
    private HookProc? _keyboardProc;
    private HookProc? _mouseProc;
    private IntPtr _keyboardHook = IntPtr.Zero;
    private IntPtr _mouseHook = IntPtr.Zero;

    private readonly List<(long ElapsedMs, MacroAction Action)> _raw = new();
    private readonly object _lock = new();
    private DateTime _start;
    private DateTime _lastMouseMove = DateTime.MinValue;
    private bool _captureMouseMoves;
    private int _stopVk;
    private TaskCompletionSource<bool>? _stopSignal;

    private const int MouseMoveThrottleMs = 20; // ~50 évènements/seconde maximum : assez fluide, sans exploser la taille du script.

    public async Task<MacroRecordingResult> RecordAsync(int maxDurationSeconds, MacroControlKey stopKey, bool captureMouseMoves,
        IProgress<TimeSpan>? progress, CancellationToken ct = default)
    {
        lock (_lock) { _raw.Clear(); }
        _captureMouseMoves = captureMouseMoves;
        _stopVk = VkOf(stopKey);
        _start = DateTime.UtcNow;
        _lastMouseMove = DateTime.MinValue;
        _stopSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _keyboardProc = KeyboardHookProc;
        _mouseProc = MouseHookProc;

        using var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
        using var currentModule = currentProcess.MainModule!;
        var moduleHandle = GetModuleHandle(currentModule.ModuleName);

        _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, moduleHandle, 0);

        var stoppedByKey = false;
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var maxDuration = TimeSpan.FromSeconds(Math.Max(1, maxDurationSeconds));
            var timeoutTask = Task.Delay(maxDuration, timeoutCts.Token);
            var progressTask = ReportProgressAsync(progress, timeoutCts.Token);

            var finished = await Task.WhenAny(_stopSignal.Task, timeoutTask).ConfigureAwait(true);
            stoppedByKey = ReferenceEquals(finished, _stopSignal.Task);

            timeoutCts.Cancel();
            try { await progressTask.ConfigureAwait(true); } catch (OperationCanceledException) { /* attendu */ }
        }
        finally
        {
            if (_keyboardHook != IntPtr.Zero) { UnhookWindowsHookEx(_keyboardHook); _keyboardHook = IntPtr.Zero; }
            if (_mouseHook != IntPtr.Zero) { UnhookWindowsHookEx(_mouseHook); _mouseHook = IntPtr.Zero; }
            _keyboardProc = null;
            _mouseProc = null;
        }

        // Les horodatages enregistrés sont relatifs au début de l'enregistrement : on les convertit en délai depuis
        // l'évènement précédent, ce que le script généré rejoue tel quel (Start-Sleep entre chaque évènement).
        List<MacroAction> actions;
        lock (_lock)
        {
            actions = new List<MacroAction>(_raw.Count);
            var previous = 0L;
            foreach (var (elapsed, action) in _raw)
            {
                action.DelayMs = (int)Math.Clamp(elapsed - previous, 0, int.MaxValue);
                previous = elapsed;
                actions.Add(action);
            }
        }

        return new MacroRecordingResult(actions, stoppedByKey);
    }

    private async Task ReportProgressAsync(IProgress<TimeSpan>? progress, CancellationToken ct)
    {
        try
        {
            while (true)
            {
                progress?.Report(DateTime.UtcNow - _start);
                await Task.Delay(200, ct).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException) { /* enregistrement terminé */ }
    }

    private void Record(MacroAction action)
    {
        var elapsed = (long)(DateTime.UtcNow - _start).TotalMilliseconds;
        lock (_lock) { _raw.Add((elapsed, action)); }
    }

    // ---------- Hooks ----------

    private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            var vk = (int)data.vkCode;
            var msg = (long)wParam;
            var isDown = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
            var isUp = msg is WM_KEYUP or WM_SYSKEYUP;

            if (vk == _stopVk && isDown)
            {
                _stopSignal?.TrySetResult(true);
                // Ne pas avaler l'évènement : il continue normalement vers la fenêtre qui a le focus (voir la doc de classe).
            }
            else if (isDown)
            {
                Record(new MacroAction { Type = MacroActionType.KeyDown, Key = vk });
            }
            else if (isUp)
            {
                Record(new MacroAction { Type = MacroActionType.KeyUp, Key = vk });
            }
        }

        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            var msg = (long)wParam;

            if (msg == WM_MOUSEMOVE)
            {
                if (_captureMouseMoves && (DateTime.UtcNow - _lastMouseMove).TotalMilliseconds >= MouseMoveThrottleMs)
                {
                    _lastMouseMove = DateTime.UtcNow;
                    Record(new MacroAction { Type = MacroActionType.MouseMove, X = data.pt.x, Y = data.pt.y });
                }
            }
            else if (msg is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN)
            {
                Record(new MacroAction { Type = MacroActionType.MouseDown, X = data.pt.x, Y = data.pt.y, Button = ButtonOf(msg) });
            }
            else if (msg is WM_LBUTTONUP or WM_RBUTTONUP or WM_MBUTTONUP)
            {
                Record(new MacroAction { Type = MacroActionType.MouseUp, X = data.pt.x, Y = data.pt.y, Button = ButtonOf(msg) });
            }
            else if (msg == WM_MOUSEWHEEL)
            {
                var delta = (short)((data.mouseData >> 16) & 0xffff);
                Record(new MacroAction { Type = MacroActionType.MouseWheel, X = data.pt.x, Y = data.pt.y, Wheel = delta });
            }
        }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private static int ButtonOf(long msg) => msg switch
    {
        WM_LBUTTONDOWN or WM_LBUTTONUP => 0,
        WM_RBUTTONDOWN or WM_RBUTTONUP => 1,
        _ => 2
    };

    private static int VkOf(MacroControlKey key) => key switch
    {
        MacroControlKey.Escape => 0x1B,
        MacroControlKey.F9 => 0x78,
        MacroControlKey.F10 => 0x79,
        MacroControlKey.F11 => 0x7A,
        MacroControlKey.F12 => 0x7B,
        MacroControlKey.Pause => 0x13,
        MacroControlKey.ScrollLock => 0x91,
        _ => 0x1B
    };

    // ---------- P/Invoke ----------

    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;

    private const long WM_KEYDOWN = 0x0100;
    private const long WM_KEYUP = 0x0101;
    private const long WM_SYSKEYDOWN = 0x0104;
    private const long WM_SYSKEYUP = 0x0105;

    private const long WM_MOUSEMOVE = 0x0200;
    private const long WM_LBUTTONDOWN = 0x0201;
    private const long WM_LBUTTONUP = 0x0202;
    private const long WM_RBUTTONDOWN = 0x0204;
    private const long WM_RBUTTONUP = 0x0205;
    private const long WM_MBUTTONDOWN = 0x0207;
    private const long WM_MBUTTONUP = 0x0208;
    private const long WM_MOUSEWHEEL = 0x020A;

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
