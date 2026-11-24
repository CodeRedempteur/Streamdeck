using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;
using StreamDeck.Services;

namespace StreamDeck.Mobile;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTask,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // L'écran reste allumé tant que l'app est au premier plan (utile pour une tablette dédiée).
        Window?.AddFlags(WindowManagerFlags.KeepScreenOn);
        HideSystemBars();
        RestoreVoiceListening();
    }

    /// <summary>
    /// L'écoute vocale était activée à la dernière fermeture de l'app (ou après un redémarrage de l'appareil) :
    /// relance le service. La permission micro a déjà été accordée la première fois (sinon Voice.Enabled n'aurait
    /// jamais pu passer à vrai) ; SetVoiceListeningAsync la redemande quand même par sécurité, sans effet si déjà accordée.
    /// </summary>
    private void RestoreVoiceListening()
    {
        try
        {
            var services = MainApplication.CurrentServices;
            var config = services?.GetService(typeof(ConfigService)) as ConfigService;
            var platform = services?.GetService(typeof(IAppPlatform)) as IAppPlatform;
            if (config?.Current.Voice.Enabled == true && platform is not null)
                _ = platform.SetVoiceListeningAsync(true);
        }
        catch (System.Exception)
        {
            // Pas grave si ça échoue au démarrage : l'utilisateur peut réactiver depuis les réglages.
        }
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (hasFocus) HideSystemBars();
    }

    /// <summary>Plein écran : cache barre d'état et barre de navigation (elles réapparaissent en glissant depuis le bord).</summary>
    private void HideSystemBars()
    {
        if (Window?.DecorView is null) return;

        var controller = WindowCompat.GetInsetsController(Window, Window.DecorView);
        if (controller is null) return;

        controller.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
        controller.Hide(WindowInsetsCompat.Type.SystemBars());
    }
}
