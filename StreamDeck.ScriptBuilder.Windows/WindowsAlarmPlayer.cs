using System.Runtime.InteropServices;
using StreamDeck.Services;

namespace StreamDeck.ScriptBuilderApp;

/// <summary>
/// v1.36.1, "sur pc ça sonne pas" : alarme réelle du minuteur sur le widget de bureau — <see cref="NoOpAlarmPlayer"/>
/// (StreamDeck.Shared) ne joue jamais rien, volontairement, en attendant une implémentation Windows ; c'est elle.
/// Utilise <c>winmm.dll</c> (<c>PlaySound</c>), une API Windows native disponible depuis toujours — pas de paquet
/// NuGet supplémentaire, pas de fichier audio à fournir : <c>SND_ALIAS</c> joue directement un son système ("Ding"
/// Windows, <c>SystemExclamation</c>), et <c>SND_LOOP</c> le fait boucler tout seul jusqu'à l'arrêt (<see cref="Stop"/>,
/// qui purge le son en cours avec <c>SND_PURGE</c>) — exactement ce qu'il faut pour une alarme qui sonne jusqu'à ce
/// qu'on clique sur la touche pour l'arrêter (voir TimerService/WidgetTimerService).
/// <para><b>Non vérifié dans cet environnement</b> : pas de session Windows avec carte son disponible ici pour
/// entendre un vrai "Ding" boucler. Écrit d'après l'API Win32 documentée (<c>winmm.dll</c>), stable depuis des
/// décennies, mais jamais exécutée ici pour de vrai — si rien ne sonne en conditions réelles, le son système
/// "Exclamation" a peut-être été désactivé dans les réglages sonores Windows (Panneau de configuration → Son) :
/// dis-le-moi, on passera à un fichier .wav embarqué plutôt qu'à un alias système.</para>
/// </summary>
internal sealed class WindowsAlarmPlayer : IAlarmPlayer
{
    [DllImport("winmm.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool PlaySound(string? pszSound, IntPtr hmod, uint fdwSound);

    private const uint SND_ASYNC = 0x0001;
    private const uint SND_LOOP = 0x0008;
    private const uint SND_NODEFAULT = 0x0002;
    private const uint SND_PURGE = 0x0040;
    private const uint SND_ALIAS = 0x00010000;

    public void PlayAlarm()
    {
        try
        {
            PlaySound("SystemExclamation", IntPtr.Zero, SND_ALIAS | SND_ASYNC | SND_LOOP | SND_NODEFAULT);
        }
        catch (Exception)
        {
            // Filet de sécurité : ne doit jamais faire planter le widget (voir WidgetTimerService/TimerService).
        }
    }

    public void Stop()
    {
        try
        {
            PlaySound(null, IntPtr.Zero, SND_PURGE);
        }
        catch (Exception)
        {
            // Déjà arrêté : sans gravité.
        }
    }
}
