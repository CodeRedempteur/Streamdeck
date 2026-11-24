using System.Runtime.InteropServices;
using Microsoft.Windows.AppNotifications;
using StreamDeck.ScriptBuilder.Services;

namespace StreamDeck.ScriptBuilderApp;

/// <summary>
/// Ouvre le widget de bureau comme une seconde fenêtre MAUI (multi-fenêtres, pris en charge par MAUI sur Windows) —
/// même processus, mêmes services (singletons) que la fenêtre principale, donc l'état (serveur en marche, etc.)
/// reste cohérent entre les deux sans rien à synchroniser.
/// </summary>
/// <remarks>
/// <para><b>Code natif Windows (WinUI / P/Invoke) — jamais testé sur un vrai Windows</b> dans l'environnement où ce
/// fichier a été écrit (Linux, sans SDK Windows) : compilé seulement par relecture attentive, vérifié seulement pour
/// respecter les signatures documentées de <c>user32.dll</c>/<c>dwmapi.dll</c> et les API publiques du Windows App
/// SDK (<c>Microsoft.UI.Windowing.AppWindow</c>).</para>
/// <para><b>Retour d'expérience pris en compte (deux essais précédents insuffisants)</b> : la première version
/// retirait la bordure sur <c>Window.HandlerChanged</c> via <c>OverlappedPresenter.SetBorderAndTitleBar(false,
/// false)</c> seul — la barre de titre restait affichée sur un vrai Windows. La deuxième a déplacé l'appel sur
/// <c>Page.Loaded</c> (plus fiable en théorie) — toujours sans effet. Cette version ajoute donc, EN PLUS de l'appel
/// WinUI, la modification directe du style de fenêtre Win32 (<c>GetWindowLong</c>/<c>SetWindowLong</c> sur
/// <c>GWL_STYLE</c>, puis <c>SetWindowPos(..., SWP_FRAMECHANGED)</c> pour forcer Windows à recalculer la zone non
/// cliente) — le mécanisme le plus bas niveau et le plus documenté pour retirer complètement la bordure/barre de
/// titre d'une fenêtre Win32, quel que soit le framework au-dessus (WinForms, WPF, WinUI, MAUI...). Si la barre de
/// titre réapparaît malgré ça, c'est que même ce niveau-là ne suffit pas dans ce contexte MAUI précis — à signaler,
/// ce sera un point de départ différent à chercher.</para>
/// <para><b>Glissement — retour en arrière après une expérience ratée</b> : la version précédente remplaçait
/// entièrement le mécanisme JS (<c>ReleaseCapture</c> + <c>SendMessage(WM_NCLBUTTONDOWN)</c>) par un sous-classement
/// de la WndProc de la fenêtre (<c>SetWindowLongPtr(GWLP_WNDPROC, ...)</c>) répondant directement à
/// <c>WM_NCHITTEST</c>, dans l'espoir d'éliminer le délai de l'aller-retour JS/.NET. Résultat sur un vrai Windows :
/// **plus aucun glissement du tout**, une régression bien pire que le défaut qu'on essayait de corriger. Cause la
/// plus probable : sous-classer la WndProc d'une fenêtre WinUI 3 qui héberge du contenu XAML (ici, le
/// <c>BlazorWebView</c>) est une opération non prise en charge — le pont d'entrée WinUI/Xaml Islands qui route les
/// messages souris vers le contenu ne passe pas forcément par le chemin classique <c>WM_*</c> qu'un sous-classement
/// Win32 traditionnel intercepte proprement, et y toucher peut casser le routage des messages pour toute la fenêtre,
/// pas seulement pour la zone qu'on visait. Le sous-classement (<c>InstallWndProcHook</c>/<c>WndProcHook</c>) est
/// donc entièrement retiré dans cette version : retour au mécanisme JS d'origine
/// (<see cref="BeginDrag"/> + <c>ReleaseCapture</c>/<c>SendMessage</c>), qui a l'inconvénient connu de ne démarrer
/// qu'au relâchement du clic, mais qui a au moins l'avantage de fonctionner. Cette approche plus invasive ne sera pas
/// retentée sans un vrai moyen de la tester sur Windows avant de la livrer.</para>
/// <para><b>Bordure blanche en redimensionnement libre — correctif retiré</b> : le correctif de la version précédente
/// pour ce problème passait par le même sous-classement WndProc que celui retiré ci-dessus (interception de
/// <c>WM_NCCALCSIZE</c>) — il part avec lui. La bordure blanche risque donc de réapparaître en mode redimensionnement
/// libre ; mieux vaut ce défaut visuel que plus aucun glissement possible. Un correctif alternatif, qui n'ait pas
/// besoin de sous-classer la fenêtre, reste à trouver.</para>
/// <para><b>Coins arrondis, deuxième mécanisme</b> : <c>DwmSetWindowAttribute(...,
/// DWMWA_WINDOW_CORNER_PREFERENCE, ...)</c> seul n'a pas suffi — un angle carré restait visible derrière l'arrondi
/// sur un vrai Windows, vraisemblablement parce que l'arrondi automatique de la DWM ne s'applique plus une fois
/// <c>WS_CAPTION</c>/<c>WS_SYSMENU</c> retirés du style de la fenêtre (voir <see cref="RemoveNativeChrome"/>). Cette
/// version ajoute donc <see cref="ApplyRoundedRegion"/> : la forme réelle de la fenêtre est découpée en rectangle
/// arrondi (<c>CreateRoundRectRgn</c> + <c>SetWindowRgn</c>), rayon mis à l'échelle du DPI
/// (<c>GetDpiForWindow</c>) et recalculé à chaque <c>Window.SizeChanged</c>. Non vérifié sur un vrai Windows, mais
/// c'est un mécanisme différent (et plus direct, au niveau de la forme de la fenêtre elle-même) de la tentative CSS
/// précédente (<c>overflow: hidden</c>), qui n'avait pas réglé le problème.</para>
/// <list type="bullet">
/// <item><b>Fermer / réduire</b> : <see cref="CloseWidget"/> utilise <c>Application.CloseWindow</c> (MAUI standard) ;
/// <see cref="MinimizeWidget"/> utilise <c>OverlappedPresenter.Minimize()</c> (API WinUI 3 publique).</item>
/// <item><b>Toujours au premier plan</b> : <c>OverlappedPresenter.IsAlwaysOnTop</c> (API WinUI 3 standard).</item>
/// <item><b>Ramener au premier plan / relancer l'application</b> : <c>AppWindow.MoveInZOrderAtTop()</c> et
/// <c>AppWindow.Show(true)</c>.</item>
/// <item><b>Redimensionnement libre</b> (<see cref="SetFreeResize"/>) : garde <c>WS_THICKFRAME</c> au lieu de le
/// retirer, active <c>OverlappedPresenter.IsResizable</c>, et élargit les bornes min/max de taille (au lieu de les
/// figer à la taille du préréglage). Non vérifié sur un vrai Windows.</item>
/// </list>
/// <para><b>Notification Windows au nouveau courrier</b> (<see cref="NotifyNewMail"/>, v1.31.0) : utilise
/// <c>Microsoft.Windows.AppNotifications.AppNotificationManager</c> (Windows App SDK), déjà présent transitivement
/// dans ce projet MAUI Windows (comme <c>Microsoft.UI.Windowing</c> ou <c>Microsoft.UI.Xaml</c> utilisés ailleurs
/// dans ce fichier) — pas de nouveau paquet NuGet ajouté. <b>Non vérifié sur un vrai Windows</b> : l'enregistrement
/// (<c>Register()</c>) et l'affichage d'une notification suivent l'usage documenté, mais jamais exécutés ici.</para>
/// <para>Dégradation prévue : chaque appel natif est protégé par un <c>try/catch</c> qui ne fait rien en cas
/// d'échec plutôt que de faire planter l'application — au pire, une option reste sans effet.</para>
/// </remarks>
public class WindowsWidgetHost(BuilderConfigService config) : IWidgetHost
{
    private const int GWL_STYLE = -16;
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;
    private const int WS_MINIMIZEBOX = 0x00020000;
    private const int WS_MAXIMIZEBOX = 0x00010000;
    private const int WS_SYSMENU = 0x00080000;

    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWC_DEFAULT = 0;
    private const int DWMWC_ROUND = 2;

    private const uint WM_NCLBUTTONDOWN = 0x00A1;
    private const int HTCAPTION = 2;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    // Découpe physique de la fenêtre en rectangle à coins arrondis (voir ApplyRoundedRegion) : DWMWA_WINDOW_CORNER_
    // PREFERENCE seul ne suffit plus une fois WS_CAPTION/WS_SYSMENU retirés (voir la remarque de classe) — Windows
    // semble alors ne plus arrondir automatiquement la fenêtre, laissant un rectangle carré visible derrière
    // l'arrondi purement visuel du CSS. SetWindowRgn force ce découpage, quoi que fasse DWM par ailleurs.
    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    // Trio classique pour faire glisser une fenêtre sans bordure depuis une zone personnalisée : on relâche la
    // capture du pointeur (ReleaseCapture), puis on fait comme si Windows venait de recevoir un clic sur une barre
    // de titre (WM_NCLBUTTONDOWN + HTCAPTION) — Windows prend alors la main sur tout le déplacement lui-même.
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private Microsoft.Maui.Controls.Window? _window;
    private Microsoft.UI.Windowing.AppWindow? _appWindow;
    private IntPtr _hwnd;
    private bool _notificationsRegistered;

    public bool CanOpen => true;

    public void Open()
    {
        // Déjà ouvert : on le ramène au premier plan plutôt que de rester sans rien faire.
        if (_window is not null)
        {
            BringToFront(_window);
            return;
        }

        var (presetWidth, presetHeight) = SizeOf(config.Current.WidgetSizePreset);
        var freeResize = config.Current.WidgetFreeResize;

        // En redimensionnement libre, on rouvre à la dernière taille choisie (si déjà redimensionné une fois),
        // sinon à la taille du préréglage comme point de départ.
        var width = freeResize && config.Current.WidgetCustomWidth > 0 ? config.Current.WidgetCustomWidth : presetWidth;
        var height = freeResize && config.Current.WidgetCustomHeight > 0 ? config.Current.WidgetCustomHeight : presetHeight;

        Microsoft.Maui.Controls.Window? window = null;
        var page = new WidgetPage();

        // Point d'accroche principal : Page.Loaded garantit que la vue native existe déjà (voir la remarque de
        // classe — HandlerChanged, essayé dans une version précédente, arrivait apparemment trop tôt sur un vrai
        // Windows). Rien n'empêche d'essayer HandlerChanged en plus, en secours : ApplyNativeOptions est sans
        // risque à rappeler plusieurs fois (elle réapplique juste les mêmes réglages).
        page.Loaded += (_, _) => { if (window is not null) ApplyNativeOptions(window); };

        window = new Microsoft.Maui.Controls.Window(page) { Title = "Stream Deck - Widget", Width = width, Height = height };
        ApplySizeLimits(window, freeResize, width, height);
        window.HandlerChanged += (_, _) => ApplyNativeOptions(window);

        // La découpe en coins arrondis (ApplyRoundedRegion) se fait à la taille ACTUELLE de la fenêtre : sans ce
        // ré-abonnement, redimensionner la fenêtre (redimensionnement libre, voir les réglages) garderait l'ancienne
        // découpe et referait apparaître un bord carré sur la nouvelle zone.
        window.SizeChanged += (_, _) => { if (_hwnd != IntPtr.Zero) ApplyRoundedRegion(_hwnd, config.Current.WidgetRoundedCorners); };

        if (config.Current.WidgetX >= 0) window.X = config.Current.WidgetX;
        if (config.Current.WidgetY >= 0) window.Y = config.Current.WidgetY;

        window.Destroying += (_, _) =>
        {
            try
            {
                config.Update(c =>
                {
                    c.WidgetX = window.X;
                    c.WidgetY = window.Y;
                    if (c.WidgetFreeResize) { c.WidgetCustomWidth = window.Width; c.WidgetCustomHeight = window.Height; }
                });
            }
            catch (Exception) { /* position/taille non retrouvées au prochain lancement : sans gravité */ }
            _window = null;
            _appWindow = null;
            _hwnd = IntPtr.Zero;
        };

        _window = window;
        Microsoft.Maui.Controls.Application.Current?.OpenWindow(window);
    }

    public void OpenMainApp()
    {
        var app = Microsoft.Maui.Controls.Application.Current;
        if (app is null) return;

        var existing = app.Windows.FirstOrDefault(w => w.Page is MainPage);
        if (existing is not null)
        {
            BringToFront(existing);
            return;
        }

        var window = new Microsoft.Maui.Controls.Window(new MainPage())
        {
            Title = "Stream Deck Script Builder",
            Width = 1360,
            Height = 900,
            MinimumWidth = 760,
            MinimumHeight = 560
        };
        app.OpenWindow(window);
    }

    public void CloseWidget()
    {
        if (_window is null) return;
        try { Microsoft.Maui.Controls.Application.Current?.CloseWindow(_window); }
        catch (Exception) { /* au pire, l'utilisateur peut arrêter l'application entière */ }
    }

    public void MinimizeWidget()
    {
        try
        {
            var appWindow = _appWindow ?? (_window is not null ? GetAppWindow(_window) : null);
            if (appWindow?.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                presenter.Minimize();
        }
        catch (Exception) { /* bouton sans effet si l'API n'est pas disponible */ }
    }

    public void BeginDrag()
    {
        try
        {
            var hwnd = _hwnd;
            if (hwnd == IntPtr.Zero && _window is not null)
            {
                hwnd = GetHwnd(_window);
                _hwnd = hwnd;
            }
            if (hwnd == IntPtr.Zero) return;

            // Relâche la capture du pointeur (JS/WebView2 l'a prise au pointerdown) puis simule le message que
            // Windows envoie normalement pour un clic sur une barre de titre — Windows prend la main sur le
            // déplacement à partir de là, avec sa propre boucle native (même fluidité qu'une vraie barre de titre).
            ReleaseCapture();
            SendMessage(hwnd, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
        }
        catch (Exception) { /* le widget reste immobile pour ce geste, sans planter */ }
    }

    public void NotifyNewMail(string title, string body) => Notify(title, body);

    /// <summary>
    /// Notification Windows (toast) générique — utilisée pour le nouveau courrier (<see cref="NotifyNewMail"/>,
    /// v1.31.0) et, depuis v1.34.7, pour les échéances de tâches (voir <c>TodoNotifyTarget</c> dans le widget). Sans
    /// effet si l'API de notification native n'est pas disponible ou échoue : ce qui a déclenché la notification
    /// reste de toute façon visible dans le widget, une notification manquée n'a donc rien de grave.
    /// </summary>
    public void Notify(string title, string body)
    {
        try
        {
            if (!_notificationsRegistered)
            {
                AppNotificationManager.Default.Register();
                _notificationsRegistered = true;
            }

            var safeTitle = System.Security.SecurityElement.Escape(title);
            var safeBody = System.Security.SecurityElement.Escape(body);
            var xml = $"<toast><visual><binding template=\"ToastGeneric\"><text>{safeTitle}</text><text>{safeBody}</text></binding></visual></toast>";
            AppNotificationManager.Default.Show(new AppNotification(xml));
        }
        catch (Exception)
        {
            // Pas de notification si l'API n'est pas disponible/échoue.
        }
    }

    public void SetAlwaysOnTop(bool onTop)
    {
        config.Update(c => c.WidgetAlwaysOnTop = onTop);
        try
        {
            var appWindow = _appWindow ?? (_window is not null ? GetAppWindow(_window) : null);
            if (appWindow?.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                presenter.IsAlwaysOnTop = onTop;
        }
        catch (Exception)
        {
            // Option sans effet immédiat si l'API n'est pas disponible : la valeur reste mémorisée pour la
            // prochaine ouverture du widget de toute façon.
        }
    }

    public void SetRoundedCorners(bool rounded)
    {
        config.Update(c => c.WidgetRoundedCorners = rounded);
        try
        {
            var appWindow = _appWindow ?? (_window is not null ? GetAppWindow(_window) : null);
            if (appWindow is null) return;
            ApplyCornerPreference(appWindow, rounded);
        }
        catch (Exception) { /* la carte reste arrondie par CSS même si l'appel natif échoue */ }
    }

    public void SetFreeResize(bool free)
    {
        config.Update(c => c.WidgetFreeResize = free);
        if (_window is null) return;

        try
        {
            ApplySizeLimits(_window, free, _window.Width, _window.Height);

            var appWindow = _appWindow ?? GetAppWindow(_window);
            if (appWindow?.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                presenter.IsResizable = free;

            // Le cadre redimensionnable (WS_THICKFRAME) doit être ré-ajouté/retiré du style Win32 pour que la souris
            // reconnaisse les bords de la fenêtre comme redimensionnables — RemoveNativeChrome(hwnd, keepThickFrame)
            // gère ça ; sûr à rappeler même si la bordure a déjà été retirée une première fois à l'ouverture.
            if (_hwnd != IntPtr.Zero) RemoveNativeChrome(_hwnd, keepThickFrame: free);
        }
        catch (Exception) { /* le widget garde son mode de redimensionnement précédent plutôt que de planter */ }
    }

    /// <summary>Applique min/max en fonction du mode de redimensionnement : figé (min=max=taille du préréglage) ou libre (bornes larges).</summary>
    private static void ApplySizeLimits(Microsoft.Maui.Controls.Window window, bool freeResize, double width, double height)
    {
        if (freeResize)
        {
            window.MinimumWidth = 160;
            window.MinimumHeight = 160;
            window.MaximumWidth = 900;
            window.MaximumHeight = 900;
        }
        else
        {
            window.MinimumWidth = width;
            window.MinimumHeight = height;
            window.MaximumWidth = width;
            window.MaximumHeight = height;
        }
    }

    private static (double Width, double Height) SizeOf(string preset) => preset switch
    {
        "compact" => (220, 260),
        "large" => (320, 460),
        _ => (280, 360) // "normal"
    };

    /// <summary>Applique au widget les options natives : sans bordure, toujours au-dessus, coins arrondis. Sûr à rappeler plusieurs fois (voir la remarque de classe).</summary>
    private void ApplyNativeOptions(Microsoft.Maui.Controls.Window window)
    {
        try
        {
            if (window.Handler?.PlatformView is not Microsoft.UI.Xaml.Window winUiWindow) return;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(winUiWindow);
            _hwnd = hwnd;

            // Ceinture ET bretelles pour retirer la bordure/barre de titre : l'API WinUI (plus propre) n'a pas
            // suffi seule sur un vrai Windows lors des deux essais précédents — voir la remarque de classe. La
            // modification directe du style Win32 est le mécanisme le plus bas niveau disponible pour ça. Le cadre
            // redimensionnable (WS_THICKFRAME) est gardé si le redimensionnement libre est actif (voir SetFreeResize).
            RemoveNativeChrome(hwnd, keepThickFrame: config.Current.WidgetFreeResize);

            var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id);
            if (appWindow is null) return;
            _appWindow = appWindow;

            if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(false, false);
                presenter.IsResizable = config.Current.WidgetFreeResize;
                presenter.IsAlwaysOnTop = config.Current.WidgetAlwaysOnTop;
            }

            ApplyCornerPreference(appWindow, config.Current.WidgetRoundedCorners);
        }
        catch (Exception)
        {
            // Dégradation silencieuse : le widget garderait alors sa barre de titre Windows normale plutôt que de
            // planter — moins joli, mais toujours utilisable (glisser/fermer/réduire par la barre de titre).
        }
    }

    /// <summary>
    /// Retire la barre de titre et le menu système directement au niveau Win32 (<c>GWL_STYLE</c>) — le mécanisme le
    /// plus bas niveau et le plus fiable pour ça, indépendant de ce que l'API WinUI de plus haut niveau
    /// (<c>OverlappedPresenter</c>) applique ou pas par-dessus. <paramref name="keepThickFrame"/> garde le cadre
    /// redimensionnable (WS_THICKFRAME) quand le redimensionnement libre est actif — sans lui, la souris ne
    /// reconnaît plus les bords de la fenêtre comme redimensionnables, quoi que dise <c>OverlappedPresenter.IsResizable</c>.
    /// </summary>
    private static void RemoveNativeChrome(IntPtr hwnd, bool keepThickFrame = false)
    {
        var style = GetWindowLong(hwnd, GWL_STYLE);
        if (style == 0) return; // échec (voir GetLastError, non vérifié ici) : on préfère ne rien écrire plutôt qu'une valeur fausse

        style &= ~(WS_CAPTION | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU);
        style = keepThickFrame ? style | WS_THICKFRAME : style & ~WS_THICKFRAME;
        SetWindowLong(hwnd, GWL_STYLE, style);

        // SWP_FRAMECHANGED : force Windows à recalculer la zone non cliente maintenant que le style a changé —
        // sans ça, le changement peut ne s'appliquer visuellement qu'au prochain redimensionnement.
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
    }

    private static void ApplyCornerPreference(Microsoft.UI.Windowing.AppWindow appWindow, bool rounded)
    {
        var hwnd = Microsoft.UI.Win32Interop.GetWindowFromWindowId(appWindow.Id);
        var preference = rounded ? DWMWC_ROUND : DWMWC_DEFAULT;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
        ApplyRoundedRegion(hwnd, rounded);
    }

    /// <summary>
    /// Découpe la fenêtre elle-même (pas juste ce qu'affiche la page) en un rectangle à coins arrondis, à la taille
    /// actuelle de la fenêtre. À rappeler à chaque redimensionnement (voir l'abonnement à <c>Window.SizeChanged</c>
    /// dans <see cref="Open"/>), sinon la découpe garde la taille d'avant et redevient fausse.
    /// </summary>
    private static void ApplyRoundedRegion(IntPtr hwnd, bool rounded)
    {
        if (!rounded)
        {
            // Région nulle = fenêtre rectangulaire normale, sans rien à découper.
            SetWindowRgn(hwnd, IntPtr.Zero, true);
            return;
        }

        if (!GetWindowRect(hwnd, out var rect)) return;
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0) return;

        // GetWindowRect renvoie des pixels physiques ; le rayon CSS (16px, voir Builder.css ".widget.is-rounded")
        // est en pixels logiques — mis à l'échelle DPI de la fenêtre pour rester visuellement cohérent avec le CSS.
        double scale;
        try { scale = GetDpiForWindow(hwnd) / 96.0; } catch (Exception) { scale = 1.0; }
        var diameter = (int)Math.Round(16 * scale * 2); // CreateRoundRectRgn attend un diamètre, pas un rayon

        // La région créée devient la propriété de Windows dès qu'elle est passée à SetWindowRgn : pas besoin (et pas
        // sûr) de la détruire nous-mêmes ensuite.
        var region = CreateRoundRectRgn(0, 0, width + 1, height + 1, diameter, diameter);
        if (region != IntPtr.Zero) SetWindowRgn(hwnd, region, true);
    }

    private static void BringToFront(Microsoft.Maui.Controls.Window window)
    {
        try
        {
            var appWindow = GetAppWindow(window);
            if (appWindow is null) return;
            appWindow.Show(true);
            appWindow.MoveInZOrderAtTop();
        }
        catch (Exception)
        {
            // Au pire l'utilisateur retrouve la fenêtre via alt-tab.
        }
    }

    /// <summary>
    /// Passerelle WinUI standard pour atteindre l'<see cref="Microsoft.UI.Windowing.AppWindow"/> natif d'une fenêtre
    /// MAUI sous Windows (nécessaire pour tout ce que MAUI n'expose pas lui-même). Coûte trois appels d'interop —
    /// évité sur le chemin chaud (glisser) grâce au cache <see cref="_appWindow"/>, rempli par <see cref="ApplyNativeOptions"/>.
    /// </summary>
    private static Microsoft.UI.Windowing.AppWindow? GetAppWindow(Microsoft.Maui.Controls.Window mauiWindow)
    {
        if (mauiWindow.Handler?.PlatformView is not Microsoft.UI.Xaml.Window winUiWindow) return null;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(winUiWindow);
        var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        return Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id);
    }

    /// <summary>Handle Win32 brut d'une fenêtre MAUI — utilisé par <see cref="BeginDrag"/>, qui a besoin du HWND directement (pas de l'AppWindow) pour SendMessage.</summary>
    private static IntPtr GetHwnd(Microsoft.Maui.Controls.Window mauiWindow) =>
        mauiWindow.Handler?.PlatformView is Microsoft.UI.Xaml.Window winUiWindow
            ? WinRT.Interop.WindowNative.GetWindowHandle(winUiWindow)
            : IntPtr.Zero;
}
