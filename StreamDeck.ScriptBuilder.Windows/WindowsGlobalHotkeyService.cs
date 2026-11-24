using System.Runtime.InteropServices;
using StreamDeck.ScriptBuilder.Services;

namespace StreamDeck.ScriptBuilderApp;

/// <summary>
/// Raccourcis clavier globaux Windows (<c>RegisterHotKey</c>) : une fenêtre CACHÉE (jamais affichée, aucune icône,
/// aucune entrée dans la barre des tâches) créée sur son propre thread dédié, avec sa propre boucle de messages —
/// c'est ce thread qui reçoit <c>WM_HOTKEY</c> quand une combinaison enregistrée est appuyée n'importe où sur le PC,
/// même si cette application n'a pas le focus ou est réduite.
/// </summary>
/// <remarks>
/// <para><b>IMPORTANT — jamais testé sur un vrai Windows</b> dans l'environnement où ce fichier a été écrit (Linux,
/// sans SDK Windows) : compilé seulement par relecture attentive, vérifié seulement pour respecter les signatures
/// documentées de <c>user32.dll</c>. Points qui pourraient se comporter différemment en pratique :</para>
/// <list type="bullet">
/// <item>Certaines combinaisons (ex : Win+L, Ctrl+Alt+Suppr) sont réservées par Windows et ne s'enregistreront pas —
/// <c>RegisterHotKey</c> échoue alors silencieusement (valeur de retour ignorée ici, comme documenté dans
/// <see cref="IGlobalHotkeyService"/>) : le raccourci reste simplement inactif, sans message d'erreur visible.</item>
/// <item>Une combinaison déjà prise par un AUTRE logiciel installé sur le PC échoue de la même façon.</item>
/// <item><c>RegisterHotKey</c>/<c>UnregisterHotKey</c> sont appelés ici depuis le thread appelant (pas forcément
/// celui de la fenêtre) : documenté comme fonctionnant tant qu'un <c>hWnd</c> valide est fourni, mais jamais vérifié
/// en pratique dans ce projet.</item>
/// </list>
/// </remarks>
public sealed class WindowsGlobalHotkeyService : IGlobalHotkeyService
{
    private const uint WM_HOTKEY = 0x0312;
    private const uint WM_CLOSE = 0x0010;
    private const uint WM_DESTROY = 0x0002;
    private const uint MOD_ALT = 0x0001, MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004, MOD_WIN = 0x0008;

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public WndProcDelegate lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
        int x, int y, int w, int h, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern ushort RegisterClassExW(ref WNDCLASSEX lpwcx);
    [DllImport("user32.dll")] private static extern bool GetMessageW(out MSG lpMsg, IntPtr hWnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessageW(ref MSG lpMsg);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG lpMsg);
    [DllImport("user32.dll")] private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern void PostQuitMessage(int exitCode);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandleW(string? moduleName);

    // Gardée en champ : sinon le GC peut ramasser le delegate alors que Windows le rappelle encore (même piège que
    // WindowsMacroRecorder pour ses hooks clavier/souris).
    private readonly WndProcDelegate _wndProc;
    private readonly object _lock = new();
    private readonly Dictionary<int, string> _idToBindingId = new();
    private readonly Dictionary<string, int> _bindingIdToNativeId = new();
    private int _nextId = 1;
    private volatile IntPtr _hwnd = IntPtr.Zero;

    public bool CanRegister => true;
    public event Action<string>? Triggered;

    public WindowsGlobalHotkeyService()
    {
        _wndProc = WndProc;
        var thread = new Thread(MessageLoop) { IsBackground = true, Name = "StreamDeck-GlobalHotkeys" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        // Attend que la fenêtre existe avant de renvoyer le service prêt à l'emploi (quelques millisecondes tout au
        // plus) : sans ça, un Register() appelé juste après la construction pourrait arriver avant que _hwnd existe.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (_hwnd == IntPtr.Zero && DateTime.UtcNow < deadline) Thread.Sleep(10);
    }

    private void MessageLoop()
    {
        const string className = "SdScriptBuilderHotkeyWnd";
        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = _wndProc,
            hInstance = GetModuleHandleW(null),
            lpszClassName = className
        };
        RegisterClassExW(ref wc);

        _hwnd = CreateWindowExW(0, className, "Stream Deck Script Builder - raccourcis", 0, 0, 0, 0, 0,
            IntPtr.Zero, IntPtr.Zero, wc.hInstance, IntPtr.Zero);
        if (_hwnd == IntPtr.Zero) return;

        while (GetMessageW(out var msg, IntPtr.Zero, 0, 0))
        {
            TranslateMessage(ref msg);
            DispatchMessageW(ref msg);
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_HOTKEY:
                string? bindingId;
                lock (_lock) _idToBindingId.TryGetValue(wParam.ToInt32(), out bindingId);
                if (bindingId is not null) Triggered?.Invoke(bindingId);
                return IntPtr.Zero;

            case WM_CLOSE:
                DestroyWindow(hWnd);
                return IntPtr.Zero;

            case WM_DESTROY:
                PostQuitMessage(0);
                return IntPtr.Zero;

            default:
                return DefWindowProcW(hWnd, msg, wParam, lParam);
        }
    }

    public void Register(string bindingId, ParsedHotkey combo)
    {
        if (_hwnd == IntPtr.Zero) return;
        Unregister(bindingId);

        var vk = VkOf(combo.Key);
        if (vk == 0) return;

        uint mods = 0;
        if (combo.Ctrl) mods |= MOD_CONTROL;
        if (combo.Alt) mods |= MOD_ALT;
        if (combo.Shift) mods |= MOD_SHIFT;
        if (combo.Win) mods |= MOD_WIN;

        int id;
        lock (_lock)
        {
            id = _nextId++;
            _idToBindingId[id] = bindingId;
            _bindingIdToNativeId[bindingId] = id;
        }

        // Retour ignoré volontairement (voir la remarque de classe) : une combinaison déjà réservée par Windows ou
        // prise par un autre logiciel échoue sans qu'il y ait grand-chose d'utile à faire ici — le raccourci reste
        // juste sans effet, ce que l'utilisateur peut constater lui-même en l'essayant.
        RegisterHotKey(_hwnd, id, mods, vk);
    }

    public void Unregister(string bindingId)
    {
        int id;
        lock (_lock)
        {
            if (!_bindingIdToNativeId.TryGetValue(bindingId, out id)) return;
            _bindingIdToNativeId.Remove(bindingId);
            _idToBindingId.Remove(id);
        }

        if (_hwnd != IntPtr.Zero) UnregisterHotKey(_hwnd, id);
    }

    public void UnregisterAll()
    {
        List<string> ids;
        lock (_lock) ids = _bindingIdToNativeId.Keys.ToList();
        foreach (var id in ids) Unregister(id);
    }

    /// <summary>Jeton canonique (voir <see cref="HotkeyKeys"/>) -> code de touche Windows (VK). 0 si inconnu.</summary>
    private static uint VkOf(string key)
    {
        if (key.Length == 1)
        {
            var c = char.ToUpperInvariant(key[0]);
            if (c is >= 'A' and <= 'Z') return (uint)c;
            if (c is >= '0' and <= '9') return (uint)c;
            return 0;
        }

        if (key.Length is 2 or 3 && (key[0] == 'F' || key[0] == 'f') && int.TryParse(key.AsSpan(1), out var fn) && fn is >= 1 and <= 24)
            return (uint)(0x70 + (fn - 1));

        return key switch
        {
            "Enter" => 0x0D,
            "Escape" => 0x1B,
            "Space" => 0x20,
            "Tab" => 0x09,
            "Backspace" => 0x08,
            "Delete" => 0x2E,
            "Insert" => 0x2D,
            "Home" => 0x24,
            "End" => 0x23,
            "PageUp" => 0x21,
            "PageDown" => 0x22,
            "Up" => 0x26,
            "Down" => 0x28,
            "Left" => 0x25,
            "Right" => 0x27,
            "Pause" => 0x13,
            "ScrollLock" => 0x91,
            "CapsLock" => 0x14,
            _ => 0
        };
    }
}
