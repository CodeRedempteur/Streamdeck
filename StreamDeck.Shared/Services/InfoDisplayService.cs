using StreamDeck.Models;

namespace StreamDeck.Services;

/// <summary>
/// Pont entre l'exécution d'un bouton (<see cref="ActionService"/>, que ce soit un appui normal ou une commande
/// vocale reconnue en tâche de fond) et l'interface : quand un bouton "Affichage plein écran" cible l'appli
/// (<see cref="InfoTarget.App"/> ou <see cref="InfoTarget.Both"/>), ce service prévient qui écoute (Home.razor),
/// qui affiche la fenêtre plein écran. Sans ce service, un déclenchement vocal (depuis le service Android en tâche
/// de fond, qui n'a pas de page à lui) n'aurait aucun moyen de faire apparaître quoi que ce soit à l'écran.
/// </summary>
public class InfoDisplayService
{
    public event Action<DeckButton>? Requested;

    public void Show(DeckButton button) => Requested?.Invoke(button);
}
