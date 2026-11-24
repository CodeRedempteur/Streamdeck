namespace StreamDeck.ScriptBuilder.Services;

public record ArgumentSuggestion(string Value, string Title, string Description);

/// <summary>Arguments courants, proposés selon le programme (pour ne pas avoir à les connaître par cœur).</summary>
public static class ArgumentCatalog
{
    private static readonly (string[] Programs, ArgumentSuggestion[] Suggestions)[] Known =
    {
        (new[] { "steam" }, new[]
        {
            new ArgumentSuggestion("-silent", "Démarrer discrètement", "Steam se lance réduit, sans ouvrir sa fenêtre."),
            new ArgumentSuggestion("-bigpicture", "Mode Big Picture", "Ouvre Steam en plein écran, interface manette / salon."),
            new ArgumentSuggestion("-applaunch 251570", "Lancer un jeu", "Remplace 251570 par le numéro (AppID) du jeu : visible dans l'adresse de sa page Steam."),
        }),
        (new[] { "chrome", "msedge", "brave" }, new[]
        {
            new ArgumentSuggestion("--new-window", "Nouvelle fenêtre", "Ouvre une fenêtre neuve même si le navigateur est déjà ouvert."),
            new ArgumentSuggestion("--start-maximized", "Fenêtre maximisée", "S'ouvre en grand."),
            new ArgumentSuggestion("--start-fullscreen", "Plein écran", "S'ouvre directement en plein écran."),
            new ArgumentSuggestion("--kiosk", "Mode kiosque", "Plein écran sans aucune barre (Alt+F4 pour quitter)."),
            new ArgumentSuggestion("--app=https://exemple.ch", "Fenêtre d'application", "Ouvre un site comme une application, sans onglets ni barre d'adresse."),
            new ArgumentSuggestion("--incognito", "Navigation privée", "Chrome et Brave. Pour Edge : --inprivate."),
            new ArgumentSuggestion("--inprivate", "Navigation privée (Edge)", "Spécifique à Microsoft Edge."),
            new ArgumentSuggestion("--profile-directory=\"Default\"", "Choisir un profil", "Nom du dossier de profil (Default, Profile 1…)."),
            new ArgumentSuggestion("--autoplay-policy=no-user-gesture-required", "Lecture automatique", "Autorise les vidéos à démarrer seules."),
        }),
        (new[] { "firefox" }, new[]
        {
            new ArgumentSuggestion("-new-window", "Nouvelle fenêtre", null!),
            new ArgumentSuggestion("-kiosk", "Mode kiosque", "Plein écran sans barres."),
            new ArgumentSuggestion("-private-window", "Navigation privée", null!),
        }),
        (new[] { "vlc" }, new[]
        {
            new ArgumentSuggestion("--fullscreen", "Plein écran", null!),
            new ArgumentSuggestion("--loop", "Répéter en boucle", "Relit la liste en boucle."),
            new ArgumentSuggestion("--play-and-exit", "Quitter à la fin", "VLC se ferme quand la lecture est finie."),
            new ArgumentSuggestion("--no-video-title-show", "Sans titre à l'écran", "Cache le titre affiché au début de la vidéo."),
        }),
        (new[] { "spotify" }, new[]
        {
            new ArgumentSuggestion("--minimized", "Démarrer réduit", "Spotify se lance dans la barre des tâches."),
        }),
        (new[] { "discord" }, new[]
        {
            new ArgumentSuggestion("--start-minimized", "Démarrer réduit", "Discord se lance dans la zone de notification."),
        }),
        (new[] { "obs64", "obs32", "obs" }, new[]
        {
            new ArgumentSuggestion("--startrecording", "Lancer l'enregistrement", "Démarre l'enregistrement dès l'ouverture."),
            new ArgumentSuggestion("--startstreaming", "Lancer le direct", "Démarre le streaming dès l'ouverture."),
            new ArgumentSuggestion("--minimize-to-tray", "Démarrer réduit", "Réduit dans la zone de notification."),
            new ArgumentSuggestion("--collection \"Ma collection\"", "Choisir une collection de scènes", "Remplace par le nom exact de ta collection."),
        }),
        (new[] { "code" }, new[]
        {
            new ArgumentSuggestion("--new-window", "Nouvelle fenêtre", null!),
            new ArgumentSuggestion("--reuse-window", "Réutiliser la fenêtre", "Ouvre dans la fenêtre déjà ouverte."),
        }),
        (new[] { "powershell", "pwsh" }, new[]
        {
            new ArgumentSuggestion("-NoExit", "Garder la fenêtre ouverte", "La console reste ouverte après le script."),
            new ArgumentSuggestion("-NoProfile -ExecutionPolicy Bypass -File \"C:\\chemin\\script.ps1\"", "Exécuter un script", "Remplace le chemin par celui de ton script."),
        }),
        (new[] { "cmd" }, new[]
        {
            new ArgumentSuggestion("/k echo Bonjour", "Exécuter puis rester ouvert", "Remplace « echo Bonjour » par ta commande (/c la ferme ensuite)."),
        }),
        (new[] { "explorer" }, new[]
        {
            new ArgumentSuggestion("\"C:\\Users\"", "Ouvrir un dossier", "Remplace par le dossier voulu."),
            new ArgumentSuggestion("/select,\"C:\\chemin\\fichier.txt\"", "Montrer un fichier", "Ouvre le dossier en sélectionnant le fichier."),
        }),
    };

    private static readonly ArgumentSuggestion[] Generic =
    {
        new("\"C:\\chemin\\fichier\"", "Ouvrir un fichier avec le programme", "Le chemin du fichier à ouvrir, entre guillemets (obligatoires s'il y a des espaces)."),
    };

    /// <summary>Suggestions pour ce programme (celles du programme d'abord, puis les générales).</summary>
    public static IReadOnlyList<ArgumentSuggestion> For(string? processName)
    {
        var list = new List<ArgumentSuggestion>();
        var name = (processName ?? string.Empty).Trim();

        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];

        foreach (var (programs, suggestions) in Known)
            if (programs.Any(p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase)))
                list.AddRange(suggestions.Select(s => s with { Description = s.Description ?? string.Empty }));

        list.AddRange(Generic);
        return list;
    }

    public static bool HasSpecific(string? processName) => For(processName).Count > Generic.Length;

    /// <summary>Ajoute une suggestion aux arguments déjà saisis (sans doublon).</summary>
    public static string Append(string? current, string value)
    {
        var text = (current ?? string.Empty).Trim();
        if (text.Length == 0) return value;

        // Même option déjà présente (ex : --app=…) : on la garde telle quelle.
        var flag = value.Split(' ', '=')[0];
        if (flag.StartsWith('-') || flag.StartsWith('/'))
            if (text.Split(' ').Any(t => t.Split('=')[0].Equals(flag, StringComparison.OrdinalIgnoreCase)))
                return text;

        return text + " " + value;
    }
}
