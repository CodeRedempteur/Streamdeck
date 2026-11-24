namespace StreamDeck.ScriptBuilder.Services;

/// <summary>
/// Ouvre le widget de bureau (une petite fenêtre séparée sans bordure — voir <c>Widget.razor</c> — qui affiche
/// l'heure et permet d'éteindre/rallumer/redémarrer le serveur sans ouvrir la fenêtre principale). Implémenté par
/// l'hôte Windows : ouvrir une nouvelle fenêtre, la faire glisser, la fermer ou la réduire, ramener la fenêtre
/// principale au premier plan, ou changer des propriétés natives de la fenêtre (toujours au-dessus, coins arrondis)
/// sont des opérations au niveau de l'application (MAUI/WinUI), pas de cette bibliothèque.
/// </summary>
public interface IWidgetHost
{
    bool CanOpen { get; }

    /// <summary>Ouvre le widget (le ramène au premier plan s'il est déjà ouvert).</summary>
    void Open();

    /// <summary>Ouvre le constructeur de scripts (fenêtre principale), ou la ramène au premier plan si elle est déjà ouverte.</summary>
    void OpenMainApp();

    /// <summary>Ferme le widget — c'est le seul moyen de le fermer, puisqu'il n'a pas de barre de titre.</summary>
    void CloseWidget();

    /// <summary>Réduit le widget dans la barre des tâches.</summary>
    void MinimizeWidget();

    /// <summary>
    /// Démarre un déplacement natif de la fenêtre du widget (comme si l'utilisateur venait de cliquer sur une vraie
    /// barre de titre) — appelé au mousedown sur la poignée, puisque le widget n'a pas de barre de titre native. Une
    /// tentative de remplacer ce mécanisme par un test natif direct (WM_NCHITTEST, sous-classement de la fenêtre) a
    /// cassé tout glissement sur un vrai Windows (voir la remarque de classe de WindowsWidgetHost) et a été retirée :
    /// ce mécanisme JS -> .NET reste, avec son défaut connu (il faut relâcher le clic pour que le glissement démarre).
    /// </summary>
    void BeginDrag();

    /// <summary>Change (et mémorise) si le widget doit rester au-dessus des autres fenêtres. Sans effet si le widget n'est pas ouvert au moment de l'appel — la valeur est de toute façon appliquée à la prochaine ouverture.</summary>
    void SetAlwaysOnTop(bool onTop);

    /// <summary>Change (et mémorise) si la fenêtre du widget doit avoir des coins arrondis (Windows 11 — sans effet visible sur des versions plus anciennes, la carte affichée dans le widget reste arrondie via CSS dans tous les cas).</summary>
    void SetRoundedCorners(bool rounded);

    /// <summary>Change (et mémorise) si le widget peut être redimensionné librement à la souris (sinon, une des trois tailles fixes). Prend effet immédiatement si le widget est déjà ouvert.</summary>
    void SetFreeResize(bool free);

    /// <summary>
    /// Affiche une notification Windows (toast) — appelé quand le widget détecte un nouveau message reçu depuis la
    /// dernière actualisation du courrier. Sans effet si l'API de notification native n'est pas disponible ou
    /// échoue : le nouveau message reste de toute façon visible dans la page courrier du widget au prochain coup
    /// d'œil, une notification manquée n'a donc rien de grave.
    /// </summary>
    void NotifyNewMail(string title, string body);

    /// <summary>Notification Windows (toast) générique — mêmes garanties que <see cref="NotifyNewMail"/> (voir sa remarque), pour tout ce qui n'est pas du courrier (échéances de tâches, v1.34.7).</summary>
    void Notify(string title, string body);
}
