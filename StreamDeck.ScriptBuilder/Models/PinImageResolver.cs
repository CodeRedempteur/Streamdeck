namespace StreamDeck.ScriptBuilder.Models;

/// <summary>
/// Convertit l'image d'une touche épinglée pour l'affichage. Un lien web (<c>https://…</c>) passe tel quel. Un
/// fichier importé depuis le disque est mémorisé comme un simple chemin (<c>file://…</c>, voir Settings.razor —
/// choisi exprès pour ne pas alourdir les réglages avec une copie encodée) mais un vrai Windows a confirmé que le
/// WebView2 qui affiche le widget refuse de charger un lien <c>file://</c> depuis sa page (restriction Chromium
/// habituelle entre origines) : l'image reste blanche même avec un chemin valide. Cette classe contourne ça en lisant
/// le fichier ici, côté C# (aucune restriction d'origine ne s'applique à une lecture disque), et en le convertissant
/// à la volée en une image encodée (<c>data:</c>) — c'est CETTE version qui est envoyée à la page, jamais le chemin
/// brut. Le fichier réglage garde quand même juste le chemin (léger, cohérent avec la demande d'origine) : la
/// conversion se refait à chaque affichage, mais un petit cache (par chemin + date de modification) évite de relire
/// et ré-encoder le fichier à chaque tuile redessinée (le widget se redessine environ chaque seconde).
/// </summary>
public static class PinImageResolver
{
    private static readonly Dictionary<string, (DateTime Written, string DataUri)> Cache = new();

    public static string Resolve(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return imageUrl ?? "";
        if (!imageUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase)) return imageUrl; // lien web : rien à faire

        try
        {
            var path = new Uri(imageUrl).LocalPath;
            var info = new FileInfo(path);
            if (!info.Exists) return "";

            if (Cache.TryGetValue(imageUrl, out var cached) && cached.Written == info.LastWriteTimeUtc)
                return cached.DataUri;

            var bytes = File.ReadAllBytes(path);
            var dataUri = $"data:{MimeFromExtension(info.Extension)};base64,{Convert.ToBase64String(bytes)}";
            Cache[imageUrl] = (info.LastWriteTimeUtc, dataUri);
            return dataUri;
        }
        catch (Exception)
        {
            // Fichier introuvable/illisible/déplacé entre-temps : pas d'image plutôt qu'une image cassée.
            return "";
        }
    }

    private static string MimeFromExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        ".webp" => "image/webp",
        _ => "image/png"
    };
}
