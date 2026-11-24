namespace StreamDeck.ScriptBuilder.Models;

/// <summary>
/// Convertit la police importée pour le widget (réglage "Personnalisée") en une image de police affichable — même
/// principe et même raison que <see cref="PinImageResolver"/> pour les images de touches épinglées : le réglage
/// garde juste un chemin de fichier (léger), mais le WebView2 qui affiche le widget refuse probablement de charger un
/// lien <c>file://</c> directement (non vérifié précisément pour les polices, mais c'est la même restriction Chromium
/// que pour les images), donc le fichier est lu ici côté C# et encodé à la volée en <c>data:</c> pour l'utiliser dans
/// une règle <c>@font-face</c>. Mis en cache par chemin + date de modification pour ne pas relire le fichier à
/// chaque redessin du widget.
/// </summary>
public static class FontResolver
{
    private static readonly Dictionary<string, (DateTime Written, string DataUri)> Cache = new();

    /// <summary>Résout un chemin de police (<c>file://…</c>) en <c>data:</c> URI utilisable dans <c>@font-face { src: url(...) }</c>. Vide si le chemin est vide/introuvable.</summary>
    public static string Resolve(string? fontPath)
    {
        if (string.IsNullOrWhiteSpace(fontPath)) return "";

        try
        {
            var path = fontPath.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ? new Uri(fontPath).LocalPath : fontPath;
            var info = new FileInfo(path);
            if (!info.Exists) return "";

            if (Cache.TryGetValue(fontPath, out var cached) && cached.Written == info.LastWriteTimeUtc)
                return cached.DataUri;

            var bytes = File.ReadAllBytes(path);
            var dataUri = $"data:{MimeFromExtension(info.Extension)};base64,{Convert.ToBase64String(bytes)}";
            Cache[fontPath] = (info.LastWriteTimeUtc, dataUri);
            return dataUri;
        }
        catch (Exception)
        {
            return ""; // fichier introuvable/illisible : le widget retombe sur la police système par défaut
        }
    }

    private static string MimeFromExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".ttf" => "font/ttf",
        ".otf" => "font/otf",
        ".woff" => "font/woff",
        ".woff2" => "font/woff2",
        _ => "font/ttf"
    };
}
