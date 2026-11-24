namespace StreamDeck.ScriptBuilder.Services;

/// <summary>
/// Conversion entre le texte affiché/enregistré (« Ctrl+Alt+F1 ») et une combinaison décomposée, et capture d'une
/// combinaison depuis un évènement clavier du navigateur (Blazor <c>KeyboardEventArgs</c>) — sans JavaScript : les
/// modificateurs et le nom de la touche sont déjà exposés côté C#.
/// </summary>
public static class HotkeyKeys
{
    private static readonly HashSet<string> PureModifierKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Control", "Alt", "Shift", "Meta", "OS", "AltGraph", "ContextMenu"
    };

    /// <summary>
    /// Depuis un keydown : null si la touche relâchée est elle-même un modificateur (Ctrl seul, par exemple) — on
    /// attend la touche « normale » qui l'accompagne. Sinon la combinaison, prête à afficher ou enregistrer.
    /// </summary>
    public static ParsedHotkey? FromKeyboardEvent(bool ctrl, bool alt, bool shift, bool meta, string domKey)
    {
        if (string.IsNullOrWhiteSpace(domKey) || PureModifierKeys.Contains(domKey)) return null;

        var key = NormalizeDomKey(domKey);
        return key.Length == 0 ? null : new ParsedHotkey(ctrl, alt, shift, meta, key);
    }

    /// <summary>Nom de touche DOM (<c>KeyboardEventArgs.Key</c>) -> jeton canonique utilisé partout ici (ex : "ArrowUp" -> "Up").</summary>
    private static string NormalizeDomKey(string domKey) => domKey switch
    {
        "ArrowUp" => "Up",
        "ArrowDown" => "Down",
        "ArrowLeft" => "Left",
        "ArrowRight" => "Right",
        " " => "Space",
        _ when domKey.Length == 1 => domKey.ToUpperInvariant(),
        _ => domKey
    };

    public static string Format(ParsedHotkey combo)
    {
        var parts = new List<string>();
        if (combo.Ctrl) parts.Add("Ctrl");
        if (combo.Alt) parts.Add("Alt");
        if (combo.Shift) parts.Add("Maj");
        if (combo.Win) parts.Add("Win");
        parts.Add(combo.Key);
        return string.Join("+", parts);
    }

    /// <summary>Relit une combinaison enregistrée (voir <see cref="Format"/>). Null si le texte est vide ou invalide.</summary>
    public static ParsedHotkey? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;

        bool ctrl = false, alt = false, shift = false, win = false;
        string? key = null;

        foreach (var p in parts)
        {
            switch (p.ToLowerInvariant())
            {
                case "ctrl": ctrl = true; break;
                case "alt": alt = true; break;
                case "maj": case "shift": shift = true; break;
                case "win": win = true; break;
                default: key = p; break;
            }
        }

        return key is null ? null : new ParsedHotkey(ctrl, alt, shift, win, key);
    }
}
