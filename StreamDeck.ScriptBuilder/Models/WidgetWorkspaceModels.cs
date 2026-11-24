namespace StreamDeck.ScriptBuilder.Models;

/// <summary>
/// Un élément de contenu sur une <see cref="WidgetPageEntry"/> (v1.34.0) : une page est une LISTE ORDONNÉE de blocs,
/// pas un ensemble d'interrupteurs uniques — on peut donc empiler plusieurs blocs du même type sur une page (deux
/// boîtes mail différentes l'une sous l'autre, puis des touches, puis encore du courrier…), demandé explicitement.
/// Remplace le modèle v1.32.0/v1.33.0 où chaque type de contenu était un simple booléen sur la page (un seul possible
/// à la fois) — voir <c>BuilderConfigService.MigrateLegacyPageFlagsToBlocks</c> pour la migration d'une configuration
/// antérieure.
/// </summary>
public class WidgetPageBlock
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>"clock", "server", "mail", "pins", "weather" ou "worldclock" (les deux derniers depuis v1.34.5).</summary>
    public string Type { get; set; } = "";

    // ----- Type == "server" -----
    public bool ShowServerPower { get; set; } = true;
    public bool ShowServerRestart { get; set; } = true;
    public bool ShowServerStopMacros { get; set; } = true;

    // ----- Type == "mail" -----
    /// <summary>Id d'une <see cref="MailAccount"/> précise, ou vide/null = toutes les boîtes activées regroupées dans ce bloc.</summary>
    public string? MailAccountId { get; set; }

    /// <summary>
    /// v1.34.10 : sur la page de VEILLE (voir Widget.razor, RenderIdleBlock), ce bloc courrier affiche par défaut
    /// juste un résumé ("X message(s), Y non lu(s)") — demandé explicitement : la possibilité d'afficher plutôt la
    /// liste complète des messages (comme en veille normale), au choix par bloc. Sans effet hors veille.
    /// </summary>
    public bool IdleShowFullMail { get; set; }

    // ----- Type == "pins" -----
    /// <summary>
    /// Touches choisies pour CE bloc précis (v1.34.0) — remplace l'ancien comportement qui affichait automatiquement
    /// TOUTES les touches épinglées non masquées dès qu'une page activait "Touches épinglées". Ici, chaque bloc de
    /// touches choisit explicitement lesquelles (ids de <see cref="WidgetPin"/>) il affiche, dans l'ordre choisi —
    /// deux blocs de touches sur des pages différentes (ou la même) peuvent donc afficher des sélections différentes.
    /// </summary>
    public List<string> PinIds { get; set; } = new();
    public int PinColumns { get; set; } = 4;

    // ----- Type == "weather" (v1.34.5) -----
    /// <summary>Nom affiché ("Genève, Suisse") — choisi via une recherche (voir WeatherService.SearchLocationsAsync), qui fournit aussi les coordonnées ci-dessous.</summary>
    public string WeatherLocationName { get; set; } = "";
    public double WeatherLat { get; set; }
    public double WeatherLon { get; set; }

    // ----- Type == "worldclock" (v1.34.5) -----
    /// <summary>Nom affiché ("Tokyo") — indépendant de <see cref="WorldClockTimeZoneId"/>, pour pouvoir afficher un nom de ville plutôt que l'identifiant technique du fuseau.</summary>
    public string WorldClockLabel { get; set; } = "";

    /// <summary>Identifiant IANA du fuseau ("Asia/Tokyo") — voir <c>TimeZoneInfo.FindSystemTimeZoneById</c>, qui accepte aussi les identifiants Windows par équivalence sur .NET moderne.</summary>
    public string WorldClockTimeZoneId { get; set; } = "";

    /// <summary>Coordonnées approximatives de la région, seulement pour positionner le repère sur la vue de la planète — pas utilisées pour calculer l'heure (le fuseau ci-dessus s'en charge).</summary>
    public double WorldClockLat { get; set; }
    public double WorldClockLon { get; set; }

    // ----- Type == "todo" (v1.34.6, devenu une référence à une liste de la bibliothèque en v1.34.8) -----

    /// <summary>
    /// Id d'une <see cref="TodoListInfo"/> de la bibliothèque (v1.34.8, onglet « Tâches ») — remplace les tâches
    /// autrefois embarquées directement ici (voir <see cref="Todos"/>, gardé pour migrer une configuration
    /// antérieure). Une même liste peut être référencée par plusieurs blocs à la fois, sur des pages (ou espaces de
    /// travail) différents — pratique pour changer une liste de page : il suffit de retirer ce bloc d'une page et
    /// d'en ajouter un nouveau ailleurs avec le même <see cref="TodoListId"/>, elle reste synchronisée partout où
    /// elle apparaît plutôt que dupliquée. Null/vide tant qu'aucune liste n'a été choisie pour ce bloc.
    /// </summary>
    public string? TodoListId { get; set; }

    /// <summary>OBSOLÈTE depuis v1.34.8 (remplacé par <see cref="TodoListId"/>, qui pointe vers une <see cref="TodoListInfo"/> de la bibliothèque) — gardé uniquement pour migrer un bloc écrit par une version antérieure, voir <c>BuilderConfigService.MigrateEmbeddedTodosToLibrary</c>. Toujours vide après la migration.</summary>
    public List<TodoItem> Todos { get; set; } = new();

    // ----- Type == "calendar" (v1.37, jour affiché configurable depuis v1.37.2, demandé explicitement : "dans
    // widget on doit pouvoir parametré affiché tache du jour, dun jour particulié, lendemain, hier, jour en avant
    // etc...") -----

    /// <summary>"today" (par défaut), "offset" (voir <see cref="CalendarDayOffset"/>) ou "specific" (voir
    /// <see cref="CalendarSpecificDate"/>) — quel jour ce bloc affiche.</summary>
    public string CalendarDayMode { get; set; } = "today";

    /// <summary>CalendarDayMode == "offset" seulement : nombre de jours par rapport à aujourd'hui (-1 = hier, +1 =
    /// demain, +3 = dans 3 jours…).</summary>
    public int CalendarDayOffset { get; set; } = 1;

    /// <summary>CalendarDayMode == "specific" seulement : date fixe toujours affichée par ce bloc, quel que soit le jour réel.</summary>
    public DateOnly? CalendarSpecificDate { get; set; }

    /// <summary>v1.37.3, demandé explicitement ("permet de ne pas avoir le bouton Gérer calendrier daffiché") : vrai
    /// par défaut (comportement d'avant) — à faux, le bouton « Gérer le calendrier » n'est plus affiché dans ce bloc,
    /// pour un widget plus compact une fois la configuration faite.</summary>
    public bool CalendarShowManageButton { get; set; } = true;

    /// <summary>v1.37.4, demandé explicitement ("un moyen d'afficher uniquement... la tâche de l'heure du jour
    /// actuelle / celle d'avant et celle d'après, actualisé en temps réel") : faux par défaut (affiche tout l'emploi
    /// du temps du jour choisi, comportement d'avant) — à vrai, ce bloc affiche seulement 3 créneaux du jour ACTUEL
    /// (toujours aujourd'hui, peu importe <see cref="CalendarDayMode"/> : "en temps réel" n'a de sens que pour
    /// maintenant) : celui en cours, le précédent et le suivant, recalculés à chaque rafraîchissement du widget (déjà
    /// toutes les secondes, voir Widget.razor _clockTimer) — pas de minuteur séparé nécessaire.</summary>
    public bool CalendarFocusMode { get; set; }
}

/// <summary>
/// Une liste de tâches de la bibliothèque (v1.34.8, onglet « Tâches » — comme les scripts et les espaces de travail
/// du widget, plutôt que des tâches embarquées directement dans un bloc de page). Rangeable dans des dossiers, comme
/// les scripts (<see cref="Folder"/>, même convention de chemin que <c>ScriptStore</c> : chaîne à séparateurs "/",
/// jusqu'à 3 niveaux) — puis choisie dans un bloc "todo" du widget (voir <see cref="WidgetPageBlock.TodoListId"/>)
/// exactement comme une boîte mail est choisie dans un bloc "mail".
/// </summary>
public class TodoListInfo
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";

    /// <summary>Chemin de dossier ("Perso/Courses"), vide = à la racine — même convention que les scripts.</summary>
    public string Folder { get; set; } = "";

    public List<TodoItem> Items { get; set; } = new();
}

/// <summary>
/// Une tâche d'un bloc "todo" (v1.34.6, notifications/description/couleur en v1.34.7) : un texte, une description
/// facultative, une échéance optionnelle, un niveau (<see cref="TodoLevel"/>) et un état fait/pas fait. Voir
/// <see cref="TodoNotifyRepeat"/>/<see cref="TodoNotifyTarget"/> pour les rappels — voir
/// <c>WidgetTodoNotificationService</c> (StreamDeck.ScriptBuilder) pour ce qui les déclenche vraiment.
/// </summary>
public class TodoItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = "";

    /// <summary>Détail facultatif de la tâche (v1.34.7) — affiché dans le widget en ouvrant la tâche, pas dans la liste elle-même (qui reste compacte).</summary>
    public string Description { get; set; } = "";

    public bool Done { get; set; }

    /// <summary>Échéance optionnelle (date + heure) — null = pas d'échéance, la tâche reste une simple case à cocher.</summary>
    public DateTime? DueAt { get; set; }

    /// <summary>Niveau d'alerte (v1.34.7) — purement visuel (couleur dans le widget), n'influe pas sur les notifications.</summary>
    public TodoLevel Level { get; set; } = TodoLevel.Normal;

    // ----- Notification (v1.34.7) : sans effet si DueAt est vide (rien à notifier). -----

    /// <summary>Nombre de jours avant l'échéance pour prévenir (0 = le jour même, à l'heure de l'échéance).</summary>
    public int NotifyDaysBefore { get; set; }

    public TodoNotifyRepeat NotifyRepeat { get; set; } = TodoNotifyRepeat.None;
    public TodoNotifyTarget NotifyTarget { get; set; } = TodoNotifyTarget.Widget;

    /// <summary>
    /// Rempli par <c>WidgetTodoNotificationService</c> une fois la notification envoyée pour l'échéance ACTUELLE (pas
    /// persistée entre deux échéances d'une tâche récurrente — remise à null quand <see cref="DueAt"/> avance) — évite
    /// de renotifier en boucle tant que la tâche n'est pas faite.
    /// </summary>
    public DateTime? LastNotifiedForDueAt { get; set; }

    /// <summary>
    /// v1.34.15, demandé explicitement ("pouvoir les retirer de la liste avec une action rapide") : retirée de
    /// l'affichage du widget (<c>Widget.razor</c>, <see cref="TodoLevel"/> n'entre pas en jeu ici) sans être
    /// supprimée — la tâche et tout son historique (description, échéance, rappels) restent intacts, seulement
    /// masqués. Réactivable depuis les réglages du bloc « Liste de tâches » du widget (<c>WidgetConfigPanel.razor</c>),
    /// qui liste les tâches retirées de la liste choisie avec un bouton « Réactiver » par tâche.
    /// </summary>
    public bool Hidden { get; set; }
}

/// <summary>Niveau d'alerte d'une tâche (v1.34.7) — juste une couleur dans le widget, voir <see cref="TodoItem.Level"/>.</summary>
public enum TodoLevel
{
    Normal = 0,
    Important = 1,
    Urgent = 2
}

/// <summary>Récurrence d'une tâche à échéance (v1.34.7) — une fois notifiée, <c>WidgetTodoNotificationService</c> avance <see cref="TodoItem.DueAt"/> à l'occurrence suivante plutôt que de la remettre à zéro.</summary>
public enum TodoNotifyRepeat
{
    None = 0,
    Daily = 1,
    Weekly = 2,
    Monthly = 3
}

/// <summary>Où envoyer le rappel d'une tâche (v1.34.7).</summary>
public enum TodoNotifyTarget
{
    /// <summary>Juste mise en évidence dans le widget (comme avant v1.34.7) — pas de notification Windows.</summary>
    Widget = 0,
    Windows = 1,
    Both = 2
}

/// <summary>
/// Une page du widget au sein d'un <see cref="WidgetWorkspace"/> — compose librement son contenu comme une liste de
/// <see cref="WidgetPageBlock"/> (v1.34.0). Remplace les anciens modèles "une page = un seul type de contenu"
/// (v1.32.0 et avant, <see cref="Kind"/>) et "un booléen par type, un seul bloc de chaque" (v1.33.0, <see cref="ShowClock"/>
/// etc. ci-dessous) — champs obsolètes gardés uniquement pour migrer une configuration antérieure au premier
/// chargement (voir <c>BuilderConfigService</c>), toujours à leur valeur par défaut après la migration.
/// </summary>
public class WidgetPageEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Nom de la page, choisi librement (v1.33.0) — affiché pour repérer chaque page sans avoir à la déplier ; vide par défaut ("Page X" calculé à l'affichage).</summary>
    public string Name { get; set; } = "";

    public List<WidgetPageBlock> Blocks { get; set; } = new();

    /// <summary>OBSOLÈTE depuis v1.32.0 : "clock", "server", "mail" ou "pins" — gardé uniquement pour migrer une page écrite par une version antérieure. Toujours vide après la migration.</summary>
    public string? Kind { get; set; }

    /// <summary>OBSOLÈTE depuis v1.34.0 (remplacé par un bloc de type "clock" dans <see cref="Blocks"/>).</summary>
    public bool ShowClock { get; set; }

    /// <summary>OBSOLÈTE depuis v1.34.0 (remplacé par un bloc de type "server" dans <see cref="Blocks"/>).</summary>
    public bool ShowServerControls { get; set; }
    public bool ShowServerPower { get; set; } = true;
    public bool ShowServerRestart { get; set; } = true;
    public bool ShowServerStopMacros { get; set; } = true;

    /// <summary>OBSOLÈTE depuis v1.34.0 (remplacé par un bloc de type "mail" dans <see cref="Blocks"/>).</summary>
    public bool ShowMail { get; set; }
    public string? MailAccountId { get; set; }

    /// <summary>OBSOLÈTE depuis v1.34.0 (remplacé par un bloc de type "pins" dans <see cref="Blocks"/>).</summary>
    public bool ShowPins { get; set; }
    public int PinColumns { get; set; } = 4;
    public int PinRows { get; set; } = 2;

    // ----- Page de veille (v1.34.6) : par page plutôt qu'une seule page désignée globalement (v1.34.5,
    // WidgetIdlePageId, retiré) — sur demande explicite, pour pouvoir mettre plusieurs pages en rotation de veille. -----

    /// <summary>Cette page fait partie de la rotation affichée en mode veille (voir BuilderConfig.WidgetIdleEnabled) — plusieurs pages peuvent l'activer à la fois, elles alternent alors toutes les BuilderConfig.WidgetIdleRotateSeconds.</summary>
    public bool ShowInIdle { get; set; }

    /// <summary>Cette page n'apparaît QUE en mode veille, jamais dans le carrousel normal — sans effet si <see cref="ShowInIdle"/> est resté à faux (elle n'apparaîtrait alors nulle part).</summary>
    public bool IdleOnly { get; set; }
}

/// <summary>
/// Un espace de travail du widget : un groupe de pages nommé, dans le même esprit que les espaces/écrans d'accueil
/// de l'application mobile.
/// </summary>
/// <remarks>
/// v1.34.12, clarifié après plusieurs allers-retours : "Actif sur le widget" (voir <see cref="Active"/>) est un choix
/// MULTIPLE (plusieurs espaces peuvent l'avoir à la fois, actif par défaut) — mais ça ne fusionne PAS leurs pages
/// normales entre elles ("je dois bien avoir des workspace différents... chacun leur propre page"). Ça veut dire
/// deux choses à la fois : 1) seuls les espaces actifs apparaissent dans le sélecteur du widget (bouton "changer
/// d'espace") — un espace désactivé n'est simplement plus proposé au changement, sans être supprimé ; 2) TOUS les
/// espaces actifs contribuent leurs pages de veille (ShowInIdle) au carrousel de veille, pas seulement celui
/// actuellement affiché — cas d'usage cité depuis le début : un espace "Bureau" affiché au quotidien, un autre dédié
/// aux pages de veille, tous deux actifs. Un seul espace (parmi les actifs) reste "affiché" pour le carrousel NORMAL
/// à un instant donné (voir <c>BuilderConfig.ActiveWidgetWorkspaceId</c>), changé via le sélecteur du widget.
/// Remplace le détour v1.34.10 (tout fusionné, y compris le carrousel normal) et v1.34.11 (un seul choix exclusif,
/// plus un second interrupteur "ActiveForIdle" séparé) — un seul interrupteur, comme demandé.
/// </remarks>
public class WidgetWorkspace
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Espace";
    public List<WidgetPageEntry> Pages { get; set; } = new();

    /// <summary>
    /// v1.34.12 : actif par défaut — détermine si cet espace apparaît dans le sélecteur du widget (pour être choisi
    /// comme espace affiché) ET si ses pages de veille participent au carrousel de veille, que ce soit ou non
    /// l'espace actuellement affiché (voir Widget.razor, Carousel/IdleCarousel/ActiveWorkspace). Plusieurs espaces
    /// peuvent l'avoir à true en même temps ; au moins un doit toujours l'avoir (voir BuilderConfigService.Update).
    /// </summary>
    public bool Active { get; set; } = true;
}
