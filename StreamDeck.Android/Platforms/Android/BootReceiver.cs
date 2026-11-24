using Android.App;
using Android.Content;

namespace StreamDeck.Mobile;

/// <summary>
/// Relance l'application au démarrage de l'appareil.
/// Android 10+ n'autorise le lancement depuis l'arrière-plan que si « Afficher par-dessus les autres applications »
/// est accordé à l'app (voir le README).
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter(new[] { Intent.ActionBootCompleted })]
public class BootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent?.Action != Intent.ActionBootCompleted) return;

        var launch = new Intent(context, typeof(MainActivity));
        launch.AddFlags(ActivityFlags.NewTask);
        context.StartActivity(launch);
    }
}
