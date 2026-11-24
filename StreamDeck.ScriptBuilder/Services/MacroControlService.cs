using StreamDeck.ScriptBuilder.Generation;

namespace StreamDeck.ScriptBuilder.Services;

/// <summary>
/// Écrit le signal partagé « arrêter toutes les macros en cours » (voir <see cref="ScriptGenerator.MacroStopSignalFileName"/>) :
/// n'importe quelle macro en train de se rejouer à ce moment, quel que soit le script, le voit dans les ~20 ms qui
/// suivent et s'arrête — sans avoir besoin de connaître sa touche d'annulation individuelle. Le fichier lui-même n'a
/// pas de contenu qui compte : seule son HEURE DE MODIFICATION sert de signal (voir la génération de l'étape Macro).
/// </summary>
public class MacroControlService(BuilderConfigService config)
{
    public bool CanStop => !string.IsNullOrWhiteSpace(config.Current.ServerFolder) && Directory.Exists(config.Current.ServerFolder);

    /// <summary>Déclenche l'arrêt de toutes les macros actuellement en train de se rejouer (peu importe le script).</summary>
    public void StopAllMacros()
    {
        if (!CanStop) throw new InvalidOperationException("Le dossier du serveur n'est pas configuré (Réglages).");

        var path = Path.Combine(config.Current.ServerFolder, ScriptGenerator.MacroStopSignalFileName);
        // Le contenu n'est là que pour un humain qui ouvrirait le fichier par curiosité : seule la date de
        // modification (mise à jour par cette écriture) est lue par les macros en cours.
        File.WriteAllText(path, $"Dernier arrêt global demandé : {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
    }
}
