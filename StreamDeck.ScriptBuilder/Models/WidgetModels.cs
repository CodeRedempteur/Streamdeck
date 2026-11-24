using StreamDeck.Models;

namespace StreamDeck.ScriptBuilder.Models;

/// <summary>
/// Un script épinglé sur le widget de bureau, affiché comme une touche — même composant visuel (<c>KeyTile</c>)
/// que les touches de l'application Stream Deck, avec sa propre icône (ou image) et sa propre couleur.
/// </summary>
public class WidgetPin
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ScriptName { get; set; } = "";
    public string Icon { get; set; } = "fa-solid fa-bolt";
    public string Color { get; set; } = "#3b82f6";

    /// <summary>Image carrée à la place de l'icône (lien http(s) ou chemin de fichier importé) — ignorée si vide.</summary>
    public string ImageUrl { get; set; } = "";

    /// <summary>Si vrai, la touche n'affiche que l'image (bord à bord, sans fond ni nom) au lieu de la tuile habituelle.</summary>
    public bool ImageOnly { get; set; }

    /// <summary>Si vrai, la touche est masquée sur le widget sans être supprimée des réglages (bouton « œil »).</summary>
    public bool Hidden { get; set; }

    /// <summary>
    /// v1.35, demandé explicitement ("on doit pouvoir avoir les choix multiple sur le widget") : "script" (par
    /// défaut, lance <see cref="ScriptName"/> directement), "picker" (ouvre un popup de choix — voir
    /// <see cref="Options"/> — même composant <c>ChoicePopup</c> que l'app téléphone), ou "timer" (v1.36, minuteur/
    /// chronomètre — voir <see cref="Timer"/>). <see cref="ScriptName"/> est ignoré quand "picker" ou "timer".
    /// </summary>
    public string Kind { get; set; } = "script";

    /// <summary>Type "timer" seulement : réglages du minuteur/chronomètre — même modèle que côté téléphone
    /// (<see cref="DeckButton.Timer"/>), voir TimerConfigEditor/WidgetTimerService.</summary>
    public TimerConfig Timer { get; set; } = new();

    /// <summary>Tous types : démarre AUSSI ce timer en plus de l'action normale de la touche (ex. "gâteau" :
    /// lance le script du four ET un minuteur qui sonne à la fin). Null = pas de timer attaché.</summary>
    public TimerConfig? AttachedTimer { get; set; }

    /// <summary>
    /// Type "picker" seulement : choix proposés dans le popup — même modèle que <see cref="DeckButton.Options"/>
    /// côté téléphone, mais limité ici à des choix de type Script (exécution locale directe via <c>ScriptTester</c>,
    /// pas de PC réseau à viser — le widget tourne déjà sur le PC concerné).
    /// </summary>
    public List<ChoiceOption> Options { get; set; } = new();

    // ----- Commande vocale (v1.35, "autorisation de trigger ces bouton par la voix depuis le pc") : même principe
    // que VoiceTriggers/VoiceRequiresWakeWord sur DeckButton côté téléphone, voir Widget.razor/IPcVoiceListener. -----

    /// <summary>Phrases qui déclenchent cette touche depuis le PC (une seule doit correspondre). Vide = pas de commande vocale pour cette touche.</summary>
    public List<string> VoiceTriggers { get; set; } = new();

    /// <summary>Vrai (par défaut) : la phrase doit suivre le mot de réveil réglé dans la config du Widget. Faux : reconnue à tout moment.</summary>
    public bool VoiceRequiresWakeWord { get; set; } = true;

    /// <summary>Construit le <see cref="DeckButton"/> équivalent pour l'afficher avec <c>KeyTile</c> (toujours carré, quelle que soit l'image).</summary>
    public DeckButton ToDeckButton() => new()
    {
        Label = ScriptName,
        Kind = Kind switch { "picker" => ButtonKind.Picker, "timer" => ButtonKind.Timer, _ => ButtonKind.Script },
        Icon = string.IsNullOrWhiteSpace(Icon) ? "fa-solid fa-bolt" : Icon,
        // Résout un chemin de fichier local (file://) en image affichable — voir PinImageResolver : un lien web
        // passe tel quel, un fichier importé est lu et encodé à la volée puisque le WebView2 du widget refuse de
        // charger un file:// directement.
        ImageUrl = PinImageResolver.Resolve(ImageUrl),
        Color = string.IsNullOrWhiteSpace(Color) ? "#3b82f6" : Color,
        Enabled = true,
        Options = Options.Select(o => new ChoiceOption
        {
            Id = o.Id,
            Label = o.Label,
            Icon = o.Icon,
            ImageUrl = PinImageResolver.Resolve(o.ImageUrl),
            Color = o.Color,
            Type = o.Type,
            Script = o.Script
        }).ToList()
    };
}
