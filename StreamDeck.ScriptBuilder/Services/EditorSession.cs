using StreamDeck.ScriptBuilder.Models;

namespace StreamDeck.ScriptBuilder.Services;

/// <summary>Le script en cours de modification (partagé entre la bibliothèque et l'éditeur).</summary>
public class EditorSession
{
    public ScriptProject Project { get; private set; } = new();

    /// <summary>Nom du fichier existant (vide pour un nouveau script) : sert à détecter un renommage.</summary>
    public string OriginalName { get; private set; } = string.Empty;

    public bool IsNew => OriginalName.Length == 0;

    /// <summary>Dossier affiché dans la bibliothèque : celui où créer un nouveau script.</summary>
    public string CurrentFolder { get; set; } = string.Empty;

    public void StartNew(ScriptProject? template = null, string? folder = null)
    {
        Project = template ?? new ScriptProject();
        // Dossier demandé, sinon celui que le modèle porte déjà, sinon celui affiché dans la bibliothèque.
        Project.Folder = ScriptStore.NormalizeFolder(folder ?? (Project.Folder.Length > 0 ? Project.Folder : CurrentFolder));
        OriginalName = string.Empty;
    }

    public void Open(ScriptProject project)
    {
        Project = project;
        OriginalName = project.Name;
    }

    public void MarkSaved() => OriginalName = Project.Name.Trim();

    /// <summary>Projet de départ d'un modèle (vide si l'identifiant est inconnu).</summary>
    public static ScriptProject Template(string id) => ScriptTemplates.Find(id)?.Build() ?? new ScriptProject();
}
