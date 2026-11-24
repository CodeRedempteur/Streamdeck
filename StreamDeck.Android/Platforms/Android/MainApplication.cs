using Android.App;
using Android.Runtime;

namespace StreamDeck.Mobile;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
        // v1.34.20 : installé le plus tôt possible (avant même la construction de l'app MAUI) pour ne manquer aucun
        // plantage — voir CrashLog.
        CrashLog.Install();
    }

    /// <summary>
    /// Accès aux mêmes services (config, actions, VoiceMatcher...) que l'interface Blazor, depuis du code natif qui
    /// n'a pas d'injection de dépendances (le service d'écoute vocale). Rempli une fois l'appli MAUI construite.
    /// </summary>
    public static IServiceProvider? CurrentServices { get; private set; }

    protected override MauiApp CreateMauiApp()
    {
        var app = MauiProgram.CreateMauiApp();
        CurrentServices = app.Services;
        return app;
    }
}
