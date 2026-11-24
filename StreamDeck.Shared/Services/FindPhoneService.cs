namespace StreamDeck.Services;

/// <summary>
/// Bouton "Retrouver le téléphone" (v1.35) : pont entre <see cref="ActionService"/> (appui normal ou commande
/// vocale, même depuis le service Android en tâche de fond, qui n'a pas de page à lui) et l'interface (Home.razor),
/// comme <see cref="InfoDisplayService"/> pour l'affichage plein écran. Centralise l'état "en train de sonner" (pour
/// afficher un bouton "Arrêter" dans la barre du haut) qu'importe qui a déclenché ou arrêté la sonnerie :
/// ActionService, l'utilisateur, ou l'arrêt automatique après le délai (<see cref="IAppPlatform.TriggerFindPhone"/>).
/// </summary>
public class FindPhoneService(IAppPlatform platform, ConfigService config)
{
    // Assez long pour retrouver un téléphone dans la maison, assez court pour ne pas sonner indéfiniment si on
    // oublie de l'arrêter.
    private const int AutoStopSeconds = 60;

    public bool IsActive { get; private set; }

    public event Action? Changed;

    /// <summary>Déclenche la sonnerie. Toujours appelé depuis ActionService (appui ou voix) — jamais d'exception.</summary>
    public RunResult Trigger()
    {
        if (!platform.CanFindPhone)
            return new RunResult(false, "Cette fonction n'est pas disponible sur cet appareil");

        IsActive = true;
        Changed?.Invoke();

        try
        {
            // v1.35 : sonnerie personnalisée choisie dans les réglages, si elle existe encore sur le disque —
            // sinon repli silencieux sur la sonnerie d'alarme par défaut (voir IAppPlatform.TriggerFindPhone).
            var voice = config.Current.Voice;
            string? ringtonePath = null;
            if (!string.IsNullOrWhiteSpace(voice.SelectedRingtoneFile) && voice.CustomRingtoneFiles.Contains(voice.SelectedRingtoneFile))
                ringtonePath = platform.RingtoneFilePath(voice.SelectedRingtoneFile);

            platform.TriggerFindPhone(AutoStopSeconds, OnAutoStopped, ringtonePath);
        }
        catch (Exception)
        {
            // Filet de sécurité : ne doit jamais bloquer l'appui / la commande vocale qui a déclenché ceci.
            IsActive = false;
            Changed?.Invoke();
            return new RunResult(false, "Impossible de déclencher la sonnerie");
        }

        return new RunResult(true, "Sonnerie déclenchée");
    }

    /// <summary>Arrête la sonnerie avant le délai (bouton "Arrêter" de la barre du haut).</summary>
    public void Stop()
    {
        if (!IsActive) return;
        platform.StopFindPhone();
        IsActive = false;
        Changed?.Invoke();
    }

    private void OnAutoStopped()
    {
        IsActive = false;
        Changed?.Invoke();
    }
}
