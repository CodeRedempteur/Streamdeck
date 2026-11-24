using System.Globalization;

namespace StreamDeck.Models;

public static class ColorUtil
{
    /// <summary>Renvoie une couleur de texte lisible (sombre ou blanche) sur le fond donné.</summary>
    public static string ContrastText(string? hex)
    {
        hex = (hex ?? "").TrimStart('#');
        if (hex.Length == 3)
            hex = string.Concat(hex.Select(c => new string(c, 2)));

        if (hex.Length != 6 ||
            !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
            return "#ffffff";

        var r = (v >> 16) & 255;
        var g = (v >> 8) & 255;
        var b = v & 255;
        var luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
        return luminance > 0.6 ? "#12141a" : "#ffffff";
    }
}
