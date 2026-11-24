using System.Text.Json.Serialization;

namespace StreamDeck.ScriptBuilder.Models;

/// <summary>Ce que fait un raccourci clavier GLOBAL (marche partout sur le PC, pas seulement dans cette application).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HotkeyAction
{
    /// <summary>Lance un script (voir <see cref="HotkeyBinding.ScriptName"/>) — comme un appui sur le Stream Deck.</summary>
    TriggerScript,

    /// <summary>Arrête toutes les macros en cours de relecture, tous scripts confondus.</summary>
    StopAllMacros,

    /// <summary>Redémarre le serveur NFC (server.ps1).</summary>
    RestartServer
}

/// <summary>
/// Un raccourci clavier global (fonctionne même si cette application n'a pas le focus, voire est réduite) — réglé
/// dans les Réglages. Combinaison de touches dans <see cref="Keys"/>, au format « Ctrl+Alt+F1 » (modificateurs dans
/// n'importe quel ordre, séparés par « + », la touche à la fin).
/// </summary>
public class HotkeyBinding
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public bool Enabled { get; set; } = true;
    public HotkeyAction Action { get; set; } = HotkeyAction.TriggerScript;

    /// <summary>Uniquement pour <see cref="HotkeyAction.TriggerScript"/>.</summary>
    public string ScriptName { get; set; } = "";

    /// <summary>Ex : « Ctrl+Alt+F1 ». Vide = pas encore réglé (le raccourci reste inactif).</summary>
    public string Keys { get; set; } = "";
}

/// <summary>
/// Un raccourci qui ajoute directement une action à l'endroit où on construit un script (dans l'éditeur, pendant
/// qu'on y travaille) — pour aller plus vite qu'en passant par le menu « Ajouter une action ». Contrairement à
/// <see cref="HotkeyBinding"/>, ne marche QUE dans l'éditeur (pas besoin d'un raccourci global qui marcherait partout
/// sur le PC pour construire un script).
/// </summary>
public class QuickAddBinding
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public bool Enabled { get; set; } = true;
    public StepKind Kind { get; set; } = StepKind.OpenApp;

    /// <summary>Même format que <see cref="HotkeyBinding.Keys"/>, mais vérifié seulement pendant que l'éditeur a le focus.</summary>
    public string Keys { get; set; } = "";
}
