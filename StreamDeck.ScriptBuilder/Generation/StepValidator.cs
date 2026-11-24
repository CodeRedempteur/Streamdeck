using StreamDeck.ScriptBuilder.Models;

namespace StreamDeck.ScriptBuilder.Generation;

public enum IssueLevel { Warning, Error }

public record StepIssue(IssueLevel Level, string Message);

/// <summary>Repère ce qui manque ou paraît faux dans une étape (affiché sur la carte de l'étape).</summary>
public static class StepValidator
{
    public static List<StepIssue> Check(ScriptStep s, Func<string, bool>? fileOrFolderExists = null)
    {
        var issues = new List<StepIssue>();
        if (!s.Enabled) return issues;

        void Error(string message) => issues.Add(new StepIssue(IssueLevel.Error, message));
        void Warn(string message) => issues.Add(new StepIssue(IssueLevel.Warning, message));

        switch (s.Kind)
        {
            case StepKind.OpenApp:
                if (string.IsNullOrWhiteSpace(s.Path)) { Error("Choisis un programme, un fichier ou saisis une adresse."); break; }
                if (s.Target == TargetKind.Uri && s.Path.Trim().EndsWith("rungameid/", StringComparison.OrdinalIgnoreCase))
                    Error("Ajoute le numéro du jeu (AppID) à la fin de l'adresse, ex : steam://rungameid/251570.");
                if (s.Target != TargetKind.Uri && fileOrFolderExists is not null && !fileOrFolderExists(s.Path))
                    Warn("Ce chemin n'existe pas sur cet ordinateur (normal si le script est pour un autre PC).");
                if (s.Window.Enabled && ScriptGenerator.ProcessNameFor(s).Length == 0)
                    Error("Indique le nom du processus pour gérer sa fenêtre (ex : Steam, chrome).");
                if (s.SkipIfRunning && ScriptGenerator.ProcessNameFor(s).Length == 0)
                    Error("« Ne pas relancer s'il tourne déjà » demande le nom du processus.");
                if (s.RunAsAdmin && s.Target != TargetKind.Program)
                    Warn("« Exécuter en administrateur » ne s'applique qu'aux programmes.");
                break;

            case StepKind.OpenBrowser:
                if (string.IsNullOrWhiteSpace(s.Url) || s.Url.Trim() is "https://" or "http://")
                    Error("Saisis l'adresse de la page à ouvrir.");
                break;

            case StepKind.Window:
                if (string.IsNullOrWhiteSpace(s.ProcessName))
                    Error("Indique le nom du processus dont il faut gérer la fenêtre.");
                break;

            case StepKind.Wait:
                if (s.Seconds < 0) Error("La durée ne peut pas être négative.");
                break;

            case StepKind.Close when s.CloseTarget == CloseTarget.BrowserWindowTitle:
                if (string.IsNullOrWhiteSpace(s.WindowTitleContains))
                    Error("Indique un texte à chercher dans le titre de la fenêtre à fermer.");
                break;

            case StepKind.Close:
                if (string.IsNullOrWhiteSpace(s.ProcessName))
                    Error("Indique le nom du processus à fermer.");
                break;

            case StepKind.Spotify:
                if (s.SpotifyMode == SpotifyMode.Playlist && string.IsNullOrWhiteSpace(s.ContextUri))
                    Error("Saisis l'adresse de la playlist (spotify:playlist:…).");
                break;

            case StepKind.SendKeys:
                if (string.IsNullOrEmpty(s.Keys)) Error("Saisis les touches à envoyer.");
                break;

            case StepKind.Custom:
                if (string.IsNullOrWhiteSpace(s.Code)) Warn("Aucun code saisi.");
                break;

            case StepKind.Toggle:
                if (s.OnSteps.Count == 0 && s.OffSteps.Count == 0)
                    Warn("Ajoute au moins une action dans l'une des deux suites (marche ou arrêt).");
                break;

            case StepKind.Macro:
                if (s.MacroActions.Count == 0)
                    Error("Aucune action enregistrée : ouvre cette étape et enregistre une macro.");
                if (s.MacroStopKey == s.MacroAbortKey)
                    Warn("La touche d'arrêt d'enregistrement et la touche d'annulation de relecture sont les mêmes : pas gênant, mais à savoir.");
                break;
        }

        return issues;
    }

    /// <summary>Vrai si cette étape, ou l'une des actions de ses branches Basculer, a une erreur.</summary>
    public static bool HasErrors(ScriptStep step, Func<string, bool>? fileOrFolderExists = null) =>
        Check(step, fileOrFolderExists).Any(i => i.Level == IssueLevel.Error) ||
        step.OnSteps.Any(sub => HasErrors(sub, fileOrFolderExists)) ||
        step.OffSteps.Any(sub => HasErrors(sub, fileOrFolderExists));
}
