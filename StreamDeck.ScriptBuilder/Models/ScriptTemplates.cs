namespace StreamDeck.ScriptBuilder.Models;

/// <summary>Un enchaînement d'actions prêt à compléter.</summary>
public record ScriptTemplate(string Id, string Category, string Title, string Description, string Icon, Func<ScriptProject> Build);

public static class ScriptTemplates
{
    private const string SteamExe = @"C:\Program Files (x86)\Steam\steam.exe";
    private const string VlcExe = @"C:\Program Files\VideoLAN\VLC\vlc.exe";

    private static ScriptProject Project(params ScriptStep[] steps) => new() { Steps = steps.ToList() };

    private static WindowOptions Window(int screen, WindowState state, bool enabled = true) =>
        new() { Enabled = enabled, Screen = screen, State = state };

    public static readonly IReadOnlyList<ScriptTemplate> All = new List<ScriptTemplate>
    {
        // ----- Bases -----
        new("blank", "Bases", "Script vide", "Tu ajoutes les actions une par une.", "fa-solid fa-file", () => Project()),

        new("app-fullscreen", "Bases", "Programme en plein écran", "Ouvre un programme puis le met en plein écran sur le premier écran.", "fa-solid fa-expand",
            () => Project(new ScriptStep { Kind = StepKind.OpenApp, Window = Window(0, WindowState.Fullscreen) })),

        new("app-once-fullscreen", "Bases", "Lancer s'il n'est pas déjà ouvert", "Ne relance pas le programme s'il tourne, mais l'amène devant en plein écran.", "fa-solid fa-rocket",
            () => Project(new ScriptStep { Kind = StepKind.OpenApp, SkipIfRunning = true, Window = Window(0, WindowState.Fullscreen) })),

        new("website-fullscreen", "Bases", "Site web en plein écran", "Ouvre une page dans une nouvelle fenêtre, en plein écran.", "fa-solid fa-globe",
            () => Project(new ScriptStep { Kind = StepKind.OpenBrowser, Window = Window(0, WindowState.Fullscreen, true) })),

        new("bring-front", "Bases", "Amener une fenêtre devant", "Ramène au premier plan un programme déjà ouvert.", "fa-solid fa-window-restore",
            () => Project(new ScriptStep { Kind = StepKind.Window, Window = Window(WindowOptions.ScreenKeep, WindowState.Restore) })),

        new("open-folder-file", "Bases", "Ouvrir un dossier et un fichier", "Ouvre un dossier dans l'Explorateur puis un document.", "fa-solid fa-folder-open",
            () => Project(
                new ScriptStep { Kind = StepKind.OpenApp, Target = TargetKind.Document },
                new ScriptStep { Kind = StepKind.OpenApp, Target = TargetKind.Document })),

        // ----- Jeux et médias -----
        new("steam-game", "Jeux et médias", "Lancer un jeu Steam", "Démarre Steam s'il n'est pas ouvert, patiente, puis lance le jeu par son numéro.", "fa-brands fa-steam",
            () => Project(
                new ScriptStep { Kind = StepKind.OpenApp, Path = SteamExe, Arguments = "-silent", SkipIfRunning = true },
                new ScriptStep { Kind = StepKind.Wait, Seconds = 8 },
                new ScriptStep { Kind = StepKind.OpenApp, Target = TargetKind.Uri, Path = "steam://rungameid/" })),

        new("movie-night", "Jeux et médias", "Soirée cinéma", "Coupe la musique, règle le volume, puis ouvre un site de streaming en plein écran sur l'écran de droite.", "fa-solid fa-film",
            () => Project(
                new ScriptStep { Kind = StepKind.Media, MediaKey = "stop" },
                new ScriptStep { Kind = StepKind.Volume, VolumeMode = VolumeMode.Set, Level = 30 },
                new ScriptStep { Kind = StepKind.OpenBrowser, Window = Window(WindowOptions.ScreenRightmost, WindowState.Fullscreen, true) })),

        new("video-vlc", "Jeux et médias", "Vidéo en plein écran (VLC)", "Lance VLC en plein écran avec le fichier à lire.", "fa-solid fa-play",
            () => Project(new ScriptStep { Kind = StepKind.OpenApp, Path = VlcExe, Arguments = "--fullscreen \"C:\\chemin\\video.mp4\"" })),

        new("background-music", "Jeux et médias", "Musique de fond", "Lance Spotify (titres likés) à volume doux.", "fa-brands fa-spotify",
            () => Project(
                new ScriptStep { Kind = StepKind.Spotify, SpotifyMode = SpotifyMode.LikedSongs },
                new ScriptStep { Kind = StepKind.Volume, VolumeMode = VolumeMode.Set, Level = 25 })),

        new("toggle-mute", "Jeux et médias", "Couper / remettre le son", "Bascule le muet du PC.", "fa-solid fa-volume-xmark",
            () => Project(new ScriptStep { Kind = StepKind.Volume, VolumeMode = VolumeMode.ToggleMute })),

        // ----- Travail -----
        new("dual-screen", "Travail", "Deux écrans : programme + site", "Un programme maximisé sur l'écran 1 et un site web sur l'écran 2.", "fa-solid fa-display",
            () => Project(
                new ScriptStep { Kind = StepKind.OpenApp, Window = Window(0, WindowState.Maximize) },
                new ScriptStep { Kind = StepKind.OpenBrowser, Window = Window(1, WindowState.Maximize, true) })),

        new("work-session", "Travail", "Session de travail", "Ouvre ton projet, un site de référence et lance une musique discrète.", "fa-solid fa-briefcase",
            () => Project(
                new ScriptStep { Kind = StepKind.Spotify, SpotifyMode = SpotifyMode.LikedSongs },
                new ScriptStep { Kind = StepKind.OpenApp, Window = Window(0, WindowState.Maximize) },
                new ScriptStep { Kind = StepKind.OpenBrowser, Window = Window(1, WindowState.Maximize, true) },
                new ScriptStep { Kind = StepKind.Volume, VolumeMode = VolumeMode.Set, Level = 20 })),

        // ----- Rangement -----
        new("end-session", "Rangement", "Fin de session", "Ferme Steam et Spotify puis baisse le volume.", "fa-solid fa-moon",
            () => Project(
                new ScriptStep { Kind = StepKind.Close, ProcessName = "steam", Force = true },
                new ScriptStep { Kind = StepKind.Close, ProcessName = "Spotify", Force = true },
                new ScriptStep { Kind = StepKind.Volume, VolumeMode = VolumeMode.Set, Level = 15 })),

        new("night-mode", "Rangement", "Mode nuit", "Arrête la lecture et met le volume au minimum.", "fa-solid fa-bed",
            () => Project(
                new ScriptStep { Kind = StepKind.Media, MediaKey = "stop" },
                new ScriptStep { Kind = StepKind.Volume, VolumeMode = VolumeMode.Set, Level = 10 })),

        // ----- Marche / arrêt -----
        new("obs-record-toggle", "Marche / arrêt", "Basculer l'enregistrement OBS", "Premier appui : ouvre OBS (s'il n'est pas déjà lancé) et démarre l'enregistrement. Appui suivant : l'arrête.", "fa-solid fa-toggle-on",
            () =>
            {
                var toggle = new ScriptStep { Kind = StepKind.Toggle };
                toggle.OnSteps.Add(new ScriptStep
                {
                    Kind = StepKind.OpenApp, SkipIfRunning = true,
                    Window = new WindowOptions { ProcessName = "obs64" }
                });
                toggle.OnSteps.Add(new ScriptStep { Kind = StepKind.Wait, Seconds = 3 });
                toggle.OnSteps.Add(new ScriptStep { Kind = StepKind.SendKeys });
                toggle.OffSteps.Add(new ScriptStep { Kind = StepKind.SendKeys });
                return Project(toggle);
            }),

        new("toggle-fullscreen-page", "Marche / arrêt", "Ouvrir / fermer une page", "Premier appui : ouvre un site en plein écran. Appui suivant : ferme le navigateur.", "fa-solid fa-toggle-on",
            () =>
            {
                var toggle = new ScriptStep { Kind = StepKind.Toggle };
                toggle.OnSteps.Add(new ScriptStep { Kind = StepKind.OpenBrowser, Window = Window(0, WindowState.Fullscreen, true) });
                toggle.OffSteps.Add(new ScriptStep { Kind = StepKind.Close, ProcessName = "chrome", Force = false });
                return Project(toggle);
            }),

        // ----- Téléphone (v1.36.1 : l'étape "Téléphone", v1.34.13, n'avait encore aucun modèle proposé ici) -----
        new("find-my-phone", "Téléphone", "Faire sonner mon téléphone", "Déclenche la sonnerie/alarme de ton téléphone depuis ce PC — même mécanisme que « Retrouver le téléphone » sur l'appli (à activer côté téléphone : Réglages → Serveur de commandes).", "fa-solid fa-mobile-screen-button",
            () => Project(new ScriptStep { Kind = StepKind.Phone, PhoneAction = PhoneAction.FindPhone })),

        new("movie-night-find-phone", "Téléphone", "Avant le film : muet + retrouver le téléphone", "Coupe le son du PC puis fait sonner ton téléphone pour le retrouver avant de lancer la séance.", "fa-solid fa-film",
            () => Project(
                new ScriptStep { Kind = StepKind.Volume, VolumeMode = VolumeMode.Mute },
                new ScriptStep { Kind = StepKind.Phone, PhoneAction = PhoneAction.FindPhone })),
    };

    public static IReadOnlyList<string> Categories => All.Select(t => t.Category).Distinct().ToList();

    public static ScriptTemplate? Find(string id) => All.FirstOrDefault(t => t.Id == id);
}
