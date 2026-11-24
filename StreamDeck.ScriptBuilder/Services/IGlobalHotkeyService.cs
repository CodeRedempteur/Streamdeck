namespace StreamDeck.ScriptBuilder.Services;

/// <summary>Une combinaison de touches déjà décomposée (voir <see cref="HotkeyKeys.Parse"/>).</summary>
public record ParsedHotkey(bool Ctrl, bool Alt, bool Shift, bool Win, string Key);

/// <summary>
/// Enregistre des raccourcis clavier GLOBAUX auprès de Windows (marchent même sans le focus, voire fenêtre réduite).
/// Implémenté par l'hôte (StreamDeck.ScriptBuilder.Windows) : rien d'équivalent en dehors de Windows.
/// </summary>
public interface IGlobalHotkeyService
{
    /// <summary>Faux si l'enregistrement de raccourcis globaux n'est pas disponible sur cette plateforme.</summary>
    bool CanRegister { get; }

    /// <summary>Un raccourci enregistré a été déclenché (l'identifiant est celui passé à <see cref="Register"/>).</summary>
    event Action<string>? Triggered;

    /// <summary>
    /// (Ré)enregistre un raccourci sous cet identifiant (une combinaison déjà prise par Windows échoue silencieusement
    /// — <see cref="Triggered"/> ne se déclenchera simplement jamais pour cet identifiant). Remplace un enregistrement
    /// précédent du même identifiant.
    /// </summary>
    void Register(string id, ParsedHotkey combo);

    void Unregister(string id);

    /// <summary>Retire tous les raccourcis enregistrés par cette application (avant de tout réenregistrer, par exemple).</summary>
    void UnregisterAll();
}
