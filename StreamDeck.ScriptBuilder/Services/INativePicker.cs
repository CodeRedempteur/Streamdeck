namespace StreamDeck.ScriptBuilder.Services;

/// <summary>
/// Sélecteur de fichier / dossier natif du système (fenêtre « Ouvrir » de Windows). Fourni par l'hôte ; s'il est absent
/// ou échoue, l'application utilise son explorateur intégré. Renvoie null si l'utilisateur annule ; lève une exception
/// si le sélecteur ne peut pas s'ouvrir.
/// </summary>
public interface INativePicker
{
    Task<string?> PickFolderAsync();

    /// <param name="extensions">Extensions proposées (ex : .exe) ; null ou vide = tous les fichiers.</param>
    Task<string?> PickFileAsync(IReadOnlyCollection<string>? extensions);
}
