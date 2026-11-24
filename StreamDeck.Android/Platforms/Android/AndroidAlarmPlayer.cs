using StreamDeck.Services;

namespace StreamDeck.Mobile;

/// <summary>
/// v1.36 : alarme par défaut d'un minuteur (TimerSoundKind.Alarm) — repris du même mécanisme déjà en place pour
/// "Retrouver le téléphone" (voir AndroidPlatform.TriggerFindPhone/StopFindPhone) : sonnerie d'alarme du système
/// (RingtoneManager, flux Alarm) jouée en boucle (MediaPlayer.Looping) jusqu'à l'arrêt.
/// <b>IMPORTANT — jamais testé</b> : comme le reste des fichiers de ce dossier, écrit d'après l'API Android
/// documentée, sans appareil/émulateur disponible ici pour vérifier en conditions réelles.
/// </summary>
internal sealed class AndroidAlarmPlayer : IAlarmPlayer
{
    private Android.Media.MediaPlayer? _player;

    public void PlayAlarm()
    {
        try
        {
            Stop();

            var context = Android.App.Application.Context;
            var uri = Android.Media.RingtoneManager.GetActualDefaultRingtoneUri(context, Android.Media.RingtoneType.Alarm)
                      ?? Android.Media.RingtoneManager.GetDefaultUri(Android.Media.RingtoneType.Alarm);
            if (uri is null) return;

            var player = new Android.Media.MediaPlayer();
            player.SetAudioStreamType(Android.Media.Stream.Alarm);
            player.SetDataSource(context, uri);
            player.Looping = true;
            player.Prepare();
            player.Start();
            _player = player;
        }
        catch (System.Exception)
        {
            // Filet de sécurité : ne doit jamais faire planter l'appli (voir ActionService/TimerService).
        }
    }

    public void Stop()
    {
        try
        {
            if (_player is null) return;
            _player.Stop();
            _player.Release();
            _player = null;
        }
        catch (System.Exception)
        {
            // Déjà arrêté, ou lecteur dans un état inattendu : sans gravité.
        }
    }
}
