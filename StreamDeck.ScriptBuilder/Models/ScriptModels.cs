using System.Text.Json.Serialization;
using StreamDeck.Models;

namespace StreamDeck.ScriptBuilder.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StepKind
{
    /// <summary>Lance un programme, un fichier, un dossier ou une adresse.</summary>
    OpenApp,

    /// <summary>Ouvre une page web dans une nouvelle fenêtre du navigateur.</summary>
    OpenBrowser,

    /// <summary>Place / maximise / met en plein écran la fenêtre d'un programme déjà lancé.</summary>
    Window,

    Wait,
    Close,
    Volume,
    Media,
    Spotify,
    SendKeys,
    Custom,

    /// <summary>Deux suites d'actions : la première au premier appui du bouton, la seconde au suivant (état mémorisé entre deux exécutions).</summary>
    Toggle,

    /// <summary>Enregistre des mouvements de souris, clics et touches, puis les rejoue à l'identique.</summary>
    Macro,

    /// <summary>v1.34.13, demandé explicitement : envoie une commande au téléphone (sonnerie/alarme, ouvrir une
    /// application) via le petit serveur du téléphone (voir PhoneServerSettings côté StreamDeck.Shared).</summary>
    Phone
}

/// <summary>Ce que l'étape « Téléphone » demande au téléphone.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PhoneAction
{
    /// <summary>Déclenche la sonnerie/alarme (même mécanisme que « Retrouver le téléphone » sur l'appli).</summary>
    FindPhone,

    /// <summary>Ouvre une application installée sur le téléphone, par son nom de paquet.</summary>
    OpenApp
}

/// <summary>Ce qu'un <see cref="MacroAction"/> représente.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MacroActionType
{
    KeyDown,
    KeyUp,
    MouseMove,
    MouseDown,
    MouseUp,
    MouseWheel
}

/// <summary>Touche utilisable pour arrêter un enregistrement en cours, ou annuler une relecture en cours : un choix
/// restreint à des touches rarement utiles ailleurs, pour ne pas gêner le programme qui a le focus au même moment.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MacroControlKey
{
    Escape,
    F9,
    F10,
    F11,
    F12,
    Pause,
    ScrollLock
}

/// <summary>Un évènement enregistré (touche ou souris), avec le délai depuis l'évènement précédent (ou depuis le
/// début de l'enregistrement pour le tout premier).</summary>
public class MacroAction
{
    public int DelayMs { get; set; }
    public MacroActionType Type { get; set; }

    /// <summary>KeyDown/KeyUp : code de touche Windows (VK), tel qu'enregistré — rejoué tel quel, sans conversion.</summary>
    public int Key { get; set; }

    /// <summary>MouseMove/MouseDown/MouseUp : position à l'écran (pixels, coordonnées absolues).</summary>
    public int X { get; set; }
    public int Y { get; set; }

    /// <summary>MouseDown/MouseUp : 0 = gauche, 1 = droit, 2 = milieu.</summary>
    public int Button { get; set; }

    /// <summary>MouseWheel : quantité de défilement (multiples de 120, signé).</summary>
    public int Wheel { get; set; }
}

/// <summary>Ce que l'étape « Ouvrir » lance.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TargetKind
{
    /// <summary>Un programme (.exe, .bat, .lnk) avec arguments éventuels.</summary>
    Program,

    /// <summary>Un fichier ou un dossier, ouvert avec le programme par défaut.</summary>
    Document,

    /// <summary>Une adresse : https://…, steam://rungameid/…, spotify:…</summary>
    Uri
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WindowState
{
    Keep,
    Maximize,

    /// <summary>Vrai plein écran : maximise puis envoie F11.</summary>
    Fullscreen,

    Minimize,
    Restore
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum VolumeMode
{
    Set,
    Mute,
    Unmute,
    ToggleMute
}

/// <summary>Ce que l'étape « Fermer » cible.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CloseTarget
{
    /// <summary>Ferme tout le programme (tous ses processus), par son nom.</summary>
    Process,

    /// <summary>Ferme seulement la ou les fenêtres d'un navigateur dont le TITRE contient un texte donné.</summary>
    BrowserWindowTitle
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SpotifyMode
{
    LikedSongs,
    Playlist
}

/// <summary>Comment traiter la fenêtre d'un programme.</summary>
public class WindowOptions
{
    public const int ScreenKeep = -2;
    public const int ScreenRightmost = -1;

    /// <summary>Ne sert qu'à l'étape « Ouvrir » (les autres étapes gèrent toujours la fenêtre).</summary>
    public bool Enabled { get; set; }

    /// <summary>Nom du processus (sans .exe). Vide = déduit du programme lancé.</summary>
    public string ProcessName { get; set; } = "";

    /// <summary>Délai maximum d'attente de la fenêtre.</summary>
    public int WaitSeconds { get; set; } = 30;

    /// <summary>-2 = ne pas déplacer, -1 = écran le plus à droite, 0 = premier écran (de gauche à droite), 1, 2…</summary>
    public int Screen { get; set; } = ScreenKeep;

    public WindowState State { get; set; } = WindowState.Maximize;

    public bool Foreground { get; set; } = true;
}

/// <summary>Une action du script. Les champs utiles dépendent de <see cref="Kind"/>.</summary>
public class ScriptStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public StepKind Kind { get; set; }

    /// <summary>Une étape désactivée reste dans le script, en commentaire.</summary>
    public bool Enabled { get; set; } = true;

    // ----- OpenApp -----
    public TargetKind Target { get; set; } = TargetKind.Program;
    public string Path { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public bool RunAsAdmin { get; set; }

    /// <summary>Ne lance pas le programme s'il tourne déjà (nécessite le nom de processus).</summary>
    public bool SkipIfRunning { get; set; }

    public WindowOptions Window { get; set; } = new();

    // ----- OpenBrowser -----
    public string Url { get; set; } = "https://";
    public string Browser { get; set; } = "chrome";

    // ----- Wait -----
    public int Seconds { get; set; } = 5;

    // ----- Close -----
    public string ProcessName { get; set; } = "";
    public bool Force { get; set; } = true;
    public CloseTarget CloseTarget { get; set; } = CloseTarget.Process;

    /// <summary>Cible BrowserWindowTitle : texte à chercher dans le titre de la fenêtre (pas l'adresse : Chrome ne l'expose pas).</summary>
    public string WindowTitleContains { get; set; } = "";

    // ----- Volume / Media -----
    public VolumeMode VolumeMode { get; set; } = VolumeMode.Set;
    public int Level { get; set; } = 30;
    public string MediaKey { get; set; } = "playpause";

    // ----- Spotify -----
    public SpotifyMode SpotifyMode { get; set; } = SpotifyMode.LikedSongs;
    public string ContextUri { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string DeviceType { get; set; } = "";
    public bool LaunchSpotify { get; set; } = true;

    // ----- SendKeys / Custom -----
    public string Keys { get; set; } = "";
    public string Code { get; set; } = "";

    // ----- Toggle -----
    /// <summary>Actions du premier appui (état « éteint » -> « allumé »).</summary>
    public List<ScriptStep> OnSteps { get; set; } = new();

    /// <summary>Actions de l'appui suivant (état « allumé » -> « éteint »).</summary>
    public List<ScriptStep> OffSteps { get; set; } = new();

    // ----- Macro -----
    /// <summary>Évènements enregistrés (vide tant que rien n'a été enregistré).</summary>
    public List<MacroAction> MacroActions { get; set; } = new();

    /// <summary>Durée maximale d'un enregistrement (pas de la relecture), en secondes.</summary>
    public int MacroMaxDurationSeconds { get; set; } = 30;

    /// <summary>Touche qui arrête l'enregistrement en cours avant la durée maximale (jamais incluse dans le résultat,
    /// mais toujours reçue normalement par le programme qui a le focus à ce moment-là).</summary>
    public MacroControlKey MacroStopKey { get; set; } = MacroControlKey.F9;

    /// <summary>Touche qui annule une relecture en cours (vérifiée entre chaque évènement rejoué).</summary>
    public MacroControlKey MacroAbortKey { get; set; } = MacroControlKey.Escape;

    /// <summary>Faux : seuls les clics et les touches sont enregistrés (script plus léger), pas les déplacements de souris.</summary>
    public bool MacroCaptureMouseMoves { get; set; } = true;

    /// <summary>
    /// Arrête la relecture dès que la souris est déplacée à la main (position qui s'écarte de celle que la macro
    /// vient elle-même de placer) — utile surtout pour les macros qui déplacent la souris : reprendre la main dessus
    /// l'arrête, sans avoir besoin d'appuyer sur la touche d'annulation. Faux par défaut (option, pas un comportement
    /// systématique) : une macro qui ne fait que des clics/touches n'a rien à surveiller.
    /// </summary>
    public bool MacroStopOnManualMove { get; set; }

    // ----- Phone (v1.34.13) -----
    public PhoneAction PhoneAction { get; set; } = PhoneAction.FindPhone;

    /// <summary>OpenApp seulement : nom de paquet Android (ex : com.spotify.music).</summary>
    public string PhoneAppPackage { get; set; } = "";

    /// <summary>
    /// v1.34.21, demandé explicitement ("ajouté plusieurs téléphone... mais pas le paramètre de tel") : quel
    /// téléphone cette étape vise (voir <see cref="BuilderConfig.PhoneTargets"/>) — même principe que
    /// <see cref="StreamDeck.Models.DeckButton.PcId"/> côté boutons. Null = le téléphone par défaut
    /// (<see cref="BuilderConfig.DefaultPhoneId"/>) au moment de la GÉNÉRATION du script (l'adresse est ensuite
    /// embarquée en dur, comme avant — changer de téléphone par défaut ensuite ne touche que les prochains scripts
    /// enregistrés). Si ce téléphone a depuis été supprimé, se rabat aussi sur le défaut.
    /// </summary>
    public Guid? PhoneId { get; set; }
}

public class ScriptProject
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public string Name { get; set; } = "";

    /// <summary>Dossier dans Script (ex : « Jeux » ou « Jeux/Steam »), vide = à la racine. La source de vérité est l'emplacement du fichier.</summary>
    public string Folder { get; set; } = "";

    public string Description { get; set; } = "";
    public List<ScriptStep> Steps { get; set; } = new();

    /// <summary>Variables que ce script attend ({nom} dans le texte des étapes) : le Stream Deck en réglera la
    /// valeur sur chaque bouton qui l'appelle. Le nom doit être un identifiant PowerShell valide (lettres/chiffres/_).</summary>
    public List<ScriptVariable> Variables { get; set; } = new();

    /// <summary>Position dans son dossier (glisser-déposer) : plus petit = plus haut/à gauche. Les scripts à égalité sont triés par nom.</summary>
    public int Order { get; set; }

    /// <summary>Icône personnalisée (classe Font Awesome, ex. "fa-solid fa-rocket"). Vide = icône par défaut selon le contenu.</summary>
    public string Icon { get; set; } = "";

    /// <summary>Couleur personnalisée (hex, ex. "#3b82f6"). Vide = couleur par défaut.</summary>
    public string Color { get; set; } = "";

    /// <summary>
    /// Nom à saisir dans le bouton Script du Stream Deck. Le dossier n'est qu'un rangement dans cette application :
    /// tous les scripts sont enregistrés directement dans Script (à plat), donc ce nom ne comprend jamais le dossier.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string Route => Name.Trim();
}

/// <summary>Une variable qu'un script attend, réglée par le Stream Deck ({nom} dans le texte d'une étape).</summary>
public class ScriptVariable
{
    public string Name { get; set; } = "";
    public VariableKind Kind { get; set; } = VariableKind.Number;

    /// <summary>Nombre seulement : bornes, et valeur par défaut si le bouton n'en règle pas.</summary>
    public int Min { get; set; } = 1;
    public int Max { get; set; } = 99;

    /// <summary>Texte seulement : longueur maximale acceptée.</summary>
    public int MaxLength { get; set; } = 60;
}

/// <summary>Présentation d'un type d'étape dans l'interface.</summary>
public record StepInfo(StepKind Kind, string Title, string Description, string Icon);

public static class StepCatalog
{
    public static readonly IReadOnlyList<StepInfo> All = new List<StepInfo>
    {
        new(StepKind.OpenApp, "Ouvrir un programme", "Lance un .exe, un fichier, un dossier ou une adresse (Steam…), puis peut gérer sa fenêtre.", "fa-solid fa-rocket"),
        new(StepKind.OpenBrowser, "Ouvrir un site web", "Nouvelle fenêtre Chrome / Edge, placée sur l'écran voulu, en plein écran si tu veux.", "fa-solid fa-globe"),
        new(StepKind.Window, "Gérer une fenêtre", "Déplace, maximise, met en plein écran ou minimise un programme déjà lancé.", "fa-solid fa-window-maximize"),
        new(StepKind.Wait, "Attendre", "Fait une pause de quelques secondes.", "fa-solid fa-hourglass-half"),
        new(StepKind.Close, "Fermer un programme", "Ferme un programme par son nom de processus.", "fa-solid fa-circle-xmark"),
        new(StepKind.Volume, "Régler le volume", "Fixe le volume du PC, coupe ou remet le son.", "fa-solid fa-volume-high"),
        new(StepKind.Media, "Touche média", "Lecture / pause, piste suivante ou précédente.", "fa-solid fa-music"),
        new(StepKind.Spotify, "Lancer Spotify", "Titres likés ou une playlist, via ton SpotifyHelper.ps1.", "fa-brands fa-spotify"),
        new(StepKind.SendKeys, "Envoyer des touches", "Tape des touches au clavier (ex : {F11}, ^s).", "fa-solid fa-keyboard"),
        new(StepKind.Custom, "Code PowerShell", "Insère tes propres lignes PowerShell telles quelles.", "fa-solid fa-code"),
        new(StepKind.Toggle, "Basculer (marche / arrêt)", "Premier appui sur le bouton : une suite d'actions. Appui suivant : une autre suite. Utile pour « lancer puis arrêter un enregistrement », ouvrir/fermer une page…", "fa-solid fa-toggle-on"),
        new(StepKind.Macro, "Macro (souris + clavier)", "Enregistre des mouvements de souris, clics et touches pendant un temps donné, puis les rejoue à l'identique.", "fa-solid fa-video"),
        new(StepKind.Phone, "Téléphone", "Sonne/alarme ton téléphone, ou ouvre une application dessus — via son petit serveur local (à activer et configurer côté téléphone et ici, dans Réglages → Téléphone).", "fa-solid fa-mobile-screen-button"),
    };

    public static StepInfo Of(StepKind kind) => All.First(i => i.Kind == kind);
}
