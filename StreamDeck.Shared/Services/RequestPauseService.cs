namespace StreamDeck.Services;

/// <summary>
/// Pause globale de l'envoi de requêtes au serveur d'un PC — actionnée par le bouton de la barre du haut
/// (Home.razor). En mémoire uniquement, pas persistée (remise à zéro à chaque lancement de l'appli). Le seul
/// endroit qui la consulte est <see cref="ScriptRunner"/> (juste avant chaque requête HTTP réelle) : tant qu'elle
/// est active, script, action Windows, réveil, curseur de volume, contrôles média... tout échoue avec un message
/// clair au lieu de partir vers le serveur, que ce soit un appui normal ou une commande vocale. La surveillance du
/// serveur (ping périodique de Home.razor) n'utilise pas HTTP et n'est donc pas concernée : le statut en ligne/hors
/// ligne continue de se mettre à jour pendant la pause.
/// </summary>
public class RequestPauseService
{
    public bool Paused { get; private set; }

    public event Action? Changed;

    public void Toggle() => Set(!Paused);

    public void Set(bool paused)
    {
        if (Paused == paused) return;
        Paused = paused;
        Changed?.Invoke();
    }
}
