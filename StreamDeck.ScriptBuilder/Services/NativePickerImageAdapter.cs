using StreamDeck.Services;

namespace StreamDeck.ScriptBuilder.Services;

/// <summary>
/// v1.34.19, correctif demandé ("l'ajout d'image... ça donne un écran noir / des fois ça crash durant
/// l'exploration de l'explorateur") : adapte l'<see cref="INativePicker"/> déjà existant de Script Builder (sélecteur
/// de fichier natif Windows — voir <c>WindowsPicker</c>, déjà utilisé pour l'image épinglée du widget, le logo de
/// signature mail, la police personnalisée) à <see cref="INativeImagePicker"/> (StreamDeck.Shared) — pour que
/// ButtonEditor/OptionEditor (réutilisés tels quels dans l'onglet « Téléphone », v1.34.16) passent par ce sélecteur
/// natif plutôt que par le composant Blazor standard <c>&lt;InputFile&gt;</c>, qui déclenche l'écran noir/le
/// plantage signalé dans la WebView de cet hôte.
/// </summary>
public class NativePickerImageAdapter(INativePicker picker) : INativeImagePicker
{
    private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" };

    public bool CanPick => true;

    public async Task<string?> PickImageAsync()
    {
        var path = await picker.PickFileAsync(ImageExtensions);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

        try
        {
            var bytes = await File.ReadAllBytesAsync(path);
            var mime = MimeFromExtension(Path.GetExtension(path));
            return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string MimeFromExtension(string ext) => ext.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        ".webp" => "image/webp",
        _ => "image/png"
    };
}
