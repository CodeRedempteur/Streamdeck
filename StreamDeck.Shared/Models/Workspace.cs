using System.Text.Json.Serialization;

namespace StreamDeck.Models;

/// <summary>Un espace de travail (Jeux, Travail, Dev…) : plusieurs pages de boutons, pour un PC donné.</summary>
public class Workspace
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Espace";
    public string Icon { get; set; } = "fa-solid fa-layer-group";

    /// <summary>Désactivé : masqué de l'écran principal (onglet et boutons), sans être supprimé.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>PC visé par les boutons de cet espace (null = PC par défaut).</summary>
    public Guid? PcId { get; set; }

    public List<DeckPage> Pages { get; set; } = new() { new DeckPage() };

    // Ancien format : la grille était réglée par espace.
    [JsonPropertyName("Columns")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LegacyColumns { get; set; }

    [JsonPropertyName("Rows")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LegacyRows { get; set; }

    /// <summary>Borne chaque page et repousse les boutons qui ne tiennent plus vers la page suivante (créée si besoin).</summary>
    public void Normalize()
    {
        if (Pages.Count == 0)
            Pages.Add(new DeckPage());

        for (var i = 0; i < Pages.Count; i++)
        {
            var page = Pages[i];
            page.ClampSettings();

            page.Layout(out var unplaced);
            if (unplaced.Count == 0) continue;

            foreach (var b in unplaced)
                page.Buttons.Remove(b);

            if (i == Pages.Count - 1)
                Pages.Add(page.NewPageLikeThis());

            Pages[i + 1].Buttons.InsertRange(0, unplaced);
        }
    }
}

/// <summary>Position d'un bouton dans la grille d'une page (indices à partir de 0).</summary>
public record Placement(DeckButton Button, int Column, int Row);

public class DeckPage
{
    public const int MaxColumns = 10;
    public const int MaxRows = 8;

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Nom facultatif ; « Page N » si vide.</summary>
    public string Name { get; set; } = "";

    /// <summary>Boutons par ligne.</summary>
    public int Columns { get; set; } = 4;

    /// <summary>Lignes.</summary>
    public int Rows { get; set; } = 2;

    public Spacing Spacing { get; set; } = Spacing.Normal;
    public IconSize IconSize { get; set; } = IconSize.Normal;

    public List<DeckButton> Buttons { get; set; } = new();

    /// <summary>Nombre de cases de la page.</summary>
    [JsonIgnore]
    public int Cells => Math.Max(1, Columns) * Math.Max(1, Rows);

    /// <summary>Cases occupées (un bouton large en occupe plusieurs).</summary>
    [JsonIgnore]
    public int UsedCells => Layout(out _).Sum(p => SpanColumns(p.Button) * SpanRows(p.Button));

    public void ClampSettings()
    {
        Columns = Math.Clamp(Columns, 1, MaxColumns);
        Rows = Math.Clamp(Rows, 1, MaxRows);
        if (!Enum.IsDefined(Spacing)) Spacing = Spacing.Normal;
        if (!Enum.IsDefined(IconSize)) IconSize = IconSize.Normal;

        foreach (var b in Buttons)
        {
            b.ColSpan = Math.Clamp(b.ColSpan, 1, Columns);
            b.RowSpan = Math.Clamp(b.RowSpan, 1, Rows);
        }
    }

    /// <summary>Nouvelle page vide, avec la même grille que celle-ci.</summary>
    public DeckPage NewPageLikeThis() => new()
    {
        Columns = Columns,
        Rows = Rows,
        Spacing = Spacing,
        IconSize = IconSize
    };

    private int SpanColumns(DeckButton b) => Math.Clamp(b.ColSpan, 1, Math.Max(1, Columns));
    private int SpanRows(DeckButton b) => Math.Clamp(b.RowSpan, 1, Math.Max(1, Rows));

    /// <summary>
    /// Place les boutons dans l'ordre : chacun prend la première zone libre (parcours ligne par ligne).
    /// Les boutons qui ne tiennent pas sont renvoyés dans <paramref name="unplaced"/>.
    /// </summary>
    public List<Placement> Layout(out List<DeckButton> unplaced)
    {
        var columns = Math.Max(1, Columns);
        var rows = Math.Max(1, Rows);
        var used = new bool[rows, columns];
        var placements = new List<Placement>();
        unplaced = new List<DeckButton>();

        foreach (var b in Buttons)
        {
            var slot = FindSlot(used, columns, rows, SpanColumns(b), SpanRows(b));
            if (slot is null)
            {
                unplaced.Add(b);
                continue;
            }

            Occupy(used, slot.Value.Column, slot.Value.Row, SpanColumns(b), SpanRows(b));
            placements.Add(new Placement(b, slot.Value.Column, slot.Value.Row));
        }

        return placements;
    }

    /// <summary>Un bouton de cette taille tient-il encore sur la page ?</summary>
    public bool CanFit(int columnSpan, int rowSpan) => CanFit(columnSpan, rowSpan, null);

    /// <summary>Idem, en ignorant un bouton (utile pour agrandir un bouton existant).</summary>
    public bool CanFit(int columnSpan, int rowSpan, DeckButton? ignore)
    {
        var columns = Math.Max(1, Columns);
        var rows = Math.Max(1, Rows);
        var used = new bool[rows, columns];

        foreach (var b in Buttons)
        {
            if (b == ignore) continue;

            var slot = FindSlot(used, columns, rows, SpanColumns(b), SpanRows(b));
            if (slot is null) return false;
            Occupy(used, slot.Value.Column, slot.Value.Row, SpanColumns(b), SpanRows(b));
        }

        var cs = Math.Clamp(columnSpan, 1, columns);
        var rs = Math.Clamp(rowSpan, 1, rows);
        return FindSlot(used, columns, rows, cs, rs) is not null;
    }

    private static (int Column, int Row)? FindSlot(bool[,] used, int columns, int rows, int columnSpan, int rowSpan)
    {
        for (var r = 0; r + rowSpan <= rows; r++)
        for (var c = 0; c + columnSpan <= columns; c++)
        {
            if (IsFree(used, c, r, columnSpan, rowSpan)) return (c, r);
        }

        return null;
    }

    private static bool IsFree(bool[,] used, int column, int row, int columnSpan, int rowSpan)
    {
        for (var r = row; r < row + rowSpan; r++)
        for (var c = column; c < column + columnSpan; c++)
        {
            if (used[r, c]) return false;
        }

        return true;
    }

    private static void Occupy(bool[,] used, int column, int row, int columnSpan, int rowSpan)
    {
        for (var r = row; r < row + rowSpan; r++)
        for (var c = column; c < column + columnSpan; c++)
            used[r, c] = true;
    }
}

/// <summary>
/// Type d'une variable de script ({season}, {episode}...) : détermine comment elle se saisit.
/// </summary>
public enum VariableKind { Number, Text }

/// <summary>Quand la valeur d'une variable est décidée. Prompt : demandée juste avant de lancer, à chaque fois.
/// Fixed : réglée une fois dans les réglages du bouton, envoyée telle quelle à chaque lancement (avec
/// éventuellement <see cref="ButtonVariable.AutoIncrement"/> pour qu'elle avance toute seule après un lancement réussi).</summary>
public enum VariableMode { Fixed, Prompt }

/// <summary>
/// La valeur d'une variable de script ({name} dans le texte, côté Script Builder), telle que réglée sur CE bouton
/// (ou ce choix). Le nom doit correspondre exactement à celui déclaré dans le script.
/// </summary>
public class ButtonVariable
{
    public string Name { get; set; } = "";
    public VariableKind Kind { get; set; } = VariableKind.Number;
    public VariableMode Mode { get; set; } = VariableMode.Fixed;

    /// <summary>Valeur actuelle : utilisée telle quelle en mode Fixed ; sert de repli et de point de départ en mode Prompt.</summary>
    public string Value { get; set; } = "1";

    /// <summary>Nombre seulement : bornes proposées lors de la saisie (au lancement ou dans les réglages).</summary>
    public int Min { get; set; } = 0;
    public int Max { get; set; } = 99;

    /// <summary>Texte seulement : longueur maximale acceptée.</summary>
    public int MaxLength { get; set; } = 60;

    /// <summary>Nombre en mode Fixed seulement : avance <see cref="Value"/> de 1 après chaque lancement réussi.</summary>
    public bool AutoIncrement { get; set; }
}

public class DeckButton
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ButtonKind Kind { get; set; } = ButtonKind.Script;
    public string Label { get; set; } = "Nouveau";

    /// <summary>Désactivé : reste affiché (grisé) mais ne s'exécute plus. Pratique pour couper sans supprimer.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Type Script : nom du script tel qu'il apparaît dans l'URL (http://serveur:8765/NOMDUSCRIPT?token=...).</summary>
    public string Script { get; set; } = "";

    /// <summary>Classes Font Awesome Free (ex : "fa-solid fa-bolt"). Ignoré si <see cref="ImageUrl"/> est renseigné.</summary>
    public string Icon { get; set; } = "fa-solid fa-bolt";

    /// <summary>Image carrée à la place de l'icône : lien http(s) ou image importée (data:...;base64,...).</summary>
    public string ImageUrl { get; set; } = "";

    public string Color { get; set; } = "#3b82f6";

    /// <summary>Type Wake : adresse MAC du PC (vide = celle du PC visé).</summary>
    public string Mac { get; set; } = "";

    /// <summary>Type PcAction : identifiant de la commande (voir PcCommands).</summary>
    public string Command { get; set; } = "";

    /// <summary>PC visé par ce bouton (null = celui de l'espace).</summary>
    public Guid? PcId { get; set; }

    /// <summary>Largeur en cases.</summary>
    public int ColSpan { get; set; } = 1;

    /// <summary>Hauteur en cases.</summary>
    public int RowSpan { get; set; } = 1;

    public ConfirmMode Confirm { get; set; } = ConfirmMode.None;

    /// <summary>Type Sequence : étapes exécutées dans l'ordre.</summary>
    public List<SequenceStep> Steps { get; set; } = new();

    /// <summary>Type Picker : choix proposés dans le popup (un seul est lancé).</summary>
    public List<ChoiceOption> Options { get; set; } = new();

    // ----- Commande vocale (Android : écoute continue) -----

    /// <summary>Phrases qui déclenchent ce bouton (une seule doit correspondre). Vide = pas de commande vocale pour ce bouton.</summary>
    public List<string> VoiceTriggers { get; set; } = new();

    /// <summary>Vrai (par défaut) : la phrase doit suivre le mot de réveil. Faux : reconnue à tout moment, sans mot de réveil.</summary>
    public bool VoiceRequiresWakeWord { get; set; } = true;

    /// <summary>Variables de script ({season}, {episode}...) réglées sur ce bouton.</summary>
    public List<ButtonVariable> Variables { get; set; } = new();

    [JsonIgnore] public bool HasPromptVariables => Variables.Any(v => v.Mode == VariableMode.Prompt);

    // ----- Type InfoDisplay : affichage plein écran -----

    /// <summary>Ce qui est affiché (heure, date, jour, image).</summary>
    public InfoKind InfoKind { get; set; } = InfoKind.Clock;

    /// <summary>Où l'afficher (appli, PC, ou les deux).</summary>
    public InfoTarget InfoTarget { get; set; } = InfoTarget.App;

    /// <summary>InfoKind.Image seulement : lien (https://…), image importée (data:...;base64,...) pour l'affichage
    /// dans l'appli, ou chemin de fichier déjà présent sur le PC pour l'affichage PC. Une image importée ne peut
    /// pas être envoyée au PC (trop volumineuse pour tenir dans l'URL) : utilise un lien ou un chemin local pour ça.</summary>
    public string InfoImageUrl { get; set; } = "";

    /// <summary>Durée d'affichage en secondes, 0 = infini (fermeture manuelle seulement, avec la croix).</summary>
    public int InfoDurationSeconds { get; set; }

    // ----- InfoKind.Weather (v1.34.6) -----
    /// <summary>Nom affiché ("Genève, Suisse") — choisi via une recherche (voir WeatherService.SearchLocationsAsync), qui fournit aussi les coordonnées ci-dessous.</summary>
    public string InfoWeatherLocationName { get; set; } = "";
    public double InfoWeatherLat { get; set; }
    public double InfoWeatherLon { get; set; }

    // ----- InfoKind.WorldClock (v1.34.6) -----
    /// <summary>Nom affiché ("Tokyo") — indépendant de <see cref="InfoWorldClockTimeZoneId"/>, pour pouvoir afficher un nom de ville plutôt que l'identifiant technique du fuseau.</summary>
    public string InfoWorldClockLabel { get; set; } = "";

    /// <summary>Identifiant de fuseau (IANA ou Windows selon la plateforme) — voir <c>TimeZoneInfo.FindSystemTimeZoneById</c>.</summary>
    public string InfoWorldClockTimeZoneId { get; set; } = "";

    // ----- Type Timer : minuteur/chronomètre (v1.36) -----

    /// <summary>Type Timer seulement : réglages du minuteur/chronomètre lancé par ce bouton.</summary>
    public TimerConfig Timer { get; set; } = new();

    /// <summary>N'importe quel type (surtout Script) : démarre AUSSI ce timer en plus de l'action normale du
    /// bouton — ex. un bouton "gâteau" qui lance le script du four ET un minuteur qui sonne à la fin. Null = pas
    /// de timer attaché (comportement d'origine, inchangé).</summary>
    public TimerConfig? AttachedTimer { get; set; }

    /// <summary>Valeur de chaque variable, prête à envoyer : celles saisies au lancement (<paramref name="prompted"/>)
    /// l'emportent, les autres utilisent leur valeur réglée (<see cref="ButtonVariable.Value"/>).</summary>
    public IReadOnlyDictionary<string, string> ResolveVariables(IReadOnlyDictionary<string, string>? prompted = null)
    {
        var result = new Dictionary<string, string>();
        foreach (var v in Variables)
            result[v.Name] = prompted is not null && v.Mode == VariableMode.Prompt && prompted.TryGetValue(v.Name, out var p) ? p : v.Value;
        return result;
    }

    /// <summary>Après un lancement réussi : avance les variables Nombre + Fixed + AutoIncrement d'une unité.</summary>
    public void IncrementAutoVariables()
    {
        foreach (var v in Variables.Where(v => v.Kind == VariableKind.Number && v.Mode == VariableMode.Fixed && v.AutoIncrement))
            if (int.TryParse(v.Value, out var n)) v.Value = (n + 1).ToString();
    }
}

/// <summary>Un choix proposé par un bouton de type Picker (comme un petit bouton, sans page ni taille).</summary>
public class ChoiceOption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Label { get; set; } = "Option";
    public string Icon { get; set; } = "fa-solid fa-bolt";

    /// <summary>Image carrée à la place de l'icône : lien http(s) ou image importée. Ignore <see cref="Icon"/> si renseigné.</summary>
    public string ImageUrl { get; set; } = "";

    public string Color { get; set; } = "#3b82f6";

    public StepType Type { get; set; } = StepType.Script;

    /// <summary>Type Script : nom du script.</summary>
    public string Script { get; set; } = "";

    /// <summary>Type PcAction : identifiant de la commande.</summary>
    public string Command { get; set; } = "";

    /// <summary>Phrases qui déclenchent ce choix directement, sans passer par le popup. Vide = pas de commande vocale pour ce choix.</summary>
    public List<string> VoiceTriggers { get; set; } = new();

    /// <summary>Vrai (par défaut) : la phrase doit suivre le mot de réveil. Faux : reconnue à tout moment, sans mot de réveil.</summary>
    public bool VoiceRequiresWakeWord { get; set; } = true;

    /// <summary>Variables de script ({season}, {episode}...) réglées sur ce choix précis.</summary>
    public List<ButtonVariable> Variables { get; set; } = new();

    [JsonIgnore] public bool HasPromptVariables => Variables.Any(v => v.Mode == VariableMode.Prompt);

    /// <summary>Valeur de chaque variable, prête à envoyer : celles saisies au lancement (<paramref name="prompted"/>)
    /// l'emportent, les autres utilisent leur valeur réglée (<see cref="ButtonVariable.Value"/>).</summary>
    public IReadOnlyDictionary<string, string> ResolveVariables(IReadOnlyDictionary<string, string>? prompted = null)
    {
        var result = new Dictionary<string, string>();
        foreach (var v in Variables)
            result[v.Name] = prompted is not null && v.Mode == VariableMode.Prompt && prompted.TryGetValue(v.Name, out var p) ? p : v.Value;
        return result;
    }

    /// <summary>Après un lancement réussi : avance les variables Nombre + Fixed + AutoIncrement d'une unité.</summary>
    public void IncrementAutoVariables()
    {
        foreach (var v in Variables.Where(v => v.Kind == VariableKind.Number && v.Mode == VariableMode.Fixed && v.AutoIncrement))
            if (int.TryParse(v.Value, out var n)) v.Value = (n + 1).ToString();
    }
}

public class SequenceStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public StepType Type { get; set; } = StepType.Script;

    /// <summary>Type Script : nom du script.</summary>
    public string Script { get; set; } = "";

    /// <summary>Type Script : attendre la fin du script avant de passer à l'étape suivante.</summary>
    public bool WaitForEnd { get; set; }

    /// <summary>Type PcAction : identifiant de la commande.</summary>
    public string Command { get; set; } = "";

    /// <summary>Type WaitOnline : délai maximum ; type Delay : durée.</summary>
    public int Seconds { get; set; } = 30;
}
