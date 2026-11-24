using System.Text.Json;
using System.Text.Json.Serialization;
using StreamDeck.Models;
using StreamDeck.ScriptBuilder.Models;

namespace StreamDeck.ScriptBuilder.Services;

public class BuilderConfig
{
    /// <summary>Dossier du serveur NFC : celui qui contient server.ps1 et le dossier Script.</summary>
    public string ServerFolder { get; set; } = "";

    /// <summary>Programme utilisé pour tester les scripts (powershell.exe = Windows PowerShell, pwsh.exe = PowerShell 7).</summary>
    public string PowerShellPath { get; set; } = "powershell.exe";

    /// <summary>Redémarrer le serveur avec les droits administrateur (confirmation Windows à chaque redémarrage).</summary>
    public bool ServerRunAsAdmin { get; set; }

    /// <summary>Affichage de la bibliothèque : « cards » ou « list ».</summary>
    public string LibraryView { get; set; } = "cards";

    /// <summary>
    /// Dossiers créés dans la bibliothèque (rangement propre à cette application : pas de dossiers réels sur le disque).
    /// Garde trace des dossiers vides ; ceux qui contiennent des scripts sont de toute façon retrouvés automatiquement.
    /// </summary>
    public List<string> Collections { get; set; } = new();

    /// <summary>
    /// Rangement des scripts écrits à la main : ils n'ont pas de métadonnées où écrire un dossier (et leur fichier
    /// n'est jamais modifié par l'application), donc leur rangement est gardé ici plutôt que dans le script.
    /// </summary>
    public Dictionary<string, string> ManualFolders { get; set; } = new();

    /// <summary>Position (glisser-déposer) des scripts écrits à la main, dans le même esprit que <see cref="ManualFolders"/>.</summary>
    public Dictionary<string, int> ManualOrder { get; set; } = new();

    /// <summary>Position (glisser-déposer) des dossiers — comparable à <see cref="ManualOrder"/> pour mélanger dossiers et scripts dans un ordre unique.</summary>
    public Dictionary<string, int> FolderOrder { get; set; } = new();

    /// <summary>Dossiers désactivés : tous les scripts qu'ils contiennent sont refusés par le serveur.</summary>
    public HashSet<string> DisabledFolders { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // ----- Personnalisation visuelle -----
    // Dossiers : virtuels, pas de fichier à eux — tout vit ici. Scripts écrits à la main : jamais dans leur fichier.
    public Dictionary<string, string> FolderIcon { get; set; } = new();
    public Dictionary<string, string> FolderColor { get; set; } = new();
    public Dictionary<string, string> ManualIcon { get; set; } = new();
    public Dictionary<string, string> ManualColor { get; set; } = new();

    // ----- Zones de travail -----
    // Une zone est une entité à part (pas un dossier) : elle a son propre nom, son icône, sa couleur, son ordre.
    // La zone "Par défaut" (nom vide) n'apparaît jamais ici — elle est implicite, toujours présente.
    public List<StoredWorkspace> Workspaces { get; set; } = new();

    /// <summary>Zone d'appartenance des dossiers ET scripts de PREMIER NIVEAU uniquement (racine) — nom de zone vide
    /// ou absent = "Par défaut". Un élément niché dans un dossier suit la zone de son ancêtre de premier niveau.</summary>
    public Dictionary<string, string> RootWorkspace { get; set; } = new();

    /// <summary>Raccourcis clavier globaux (déclencher un script, arrêter toutes les macros, redémarrer le serveur…).</summary>
    public List<HotkeyBinding> Hotkeys { get; set; } = new();

    /// <summary>Raccourcis « ajout rapide » d'une action, actifs seulement pendant qu'on travaille dans l'éditeur.</summary>
    public List<QuickAddBinding> QuickAddShortcuts { get; set; } = new();

    /// <summary>Position (coin haut-gauche, pixels) du widget de bureau, mémorisée pour le rouvrir au même endroit.</summary>
    public double WidgetX { get; set; } = -1;
    public double WidgetY { get; set; } = -1;

    /// <summary>Le widget reste au-dessus des autres fenêtres (appliqué nativement — voir WindowsWidgetHost).</summary>
    public bool WidgetAlwaysOnTop { get; set; } = true;

    /// <summary>Variante visuelle du widget : "dark", "light", "glass" ou "accent".</summary>
    public string WidgetVariant { get; set; } = "dark";

    /// <summary>Police du widget : "system", "mono", "elegant" ou "custom" (importée, voir <see cref="WidgetCustomFontPath"/>).</summary>
    public string WidgetFont { get; set; } = "system";

    /// <summary>Fichier de police importé (référencé par son chemin — même principe que l'image d'une touche épinglée, voir PinImageResolver/FontResolver) pour la police "custom".</summary>
    public string WidgetCustomFontPath { get; set; } = "";

    /// <summary>Coins arrondis du widget — appliqué à la fenêtre elle-même (Windows 11) et, dans tous les cas, à la carte affichée.</summary>
    public bool WidgetRoundedCorners { get; set; } = true;

    /// <summary>Taille du widget : "compact", "normal" ou "large" — prend effet à la prochaine ouverture. Ignoré si <see cref="WidgetFreeResize"/> est actif.</summary>
    public string WidgetSizePreset { get; set; } = "normal";

    /// <summary>Redimensionnement libre à la souris (bords de la fenêtre) au lieu des trois tailles fixes.</summary>
    public bool WidgetFreeResize { get; set; }

    /// <summary>Dernière taille choisie en redimensionnement libre — mémorisée pour la retrouver au prochain lancement (-1 = jamais redimensionné, utilise la taille du préréglage).</summary>
    public double WidgetCustomWidth { get; set; } = -1;
    public double WidgetCustomHeight { get; set; } = -1;

    /// <summary>Couleurs du variant "custom" (ignorées pour les autres variantes).</summary>
    public string WidgetCustomBackground { get; set; } = "#12141B";
    public string WidgetCustomForeground { get; set; } = "#E7E9EE";

    /// <summary>Scripts épinglés au widget comme des touches (même style que l'application Stream Deck), déclenchement direct sans passer par le Stream Deck physique.</summary>
    public List<WidgetPin> WidgetPins { get; set; } = new();

    /// <summary>
    /// OBSOLÈTE depuis v1.33.0 (remplacé par <see cref="WidgetPageEntry.PinColumns"/>/<see cref="WidgetPageEntry.PinRows"/>,
    /// propres à chaque page) : gardé uniquement pour migrer une valeur personnalisée écrite par une version antérieure
    /// vers chaque page qui affiche des touches, au premier chargement.
    /// </summary>
    public int WidgetPinColumns { get; set; } = 4;
    public int WidgetPinRows { get; set; } = 2;

    /// <summary>
    /// OBSOLÈTE depuis v1.32.0 (remplacé par <see cref="WidgetClockAlwaysVisible"/>/<see cref="WidgetServerAlwaysVisible"/>
    /// + la composition libre de chaque page — voir <see cref="WidgetPageEntry"/>) : gardé uniquement pour migrer une
    /// configuration antérieure au premier chargement. Toujours vide après la migration.
    /// </summary>
    public string WidgetClockDisplay { get; set; } = "";
    public string WidgetServerControlsDisplay { get; set; } = "";

    /// <summary>
    /// OBSOLÈTE depuis v1.33.0 (l'horloge/les contrôles serveur ne s'affichent plus que si une page les ajoute
    /// explicitement — plus de réglage général/fixe en en-tête, demandé explicitement) : gardé uniquement pour migrer
    /// une configuration antérieure (une page dédiée est créée au premier chargement si c'était actif) vers le nouveau
    /// modèle. Toujours faux après la migration.
    /// </summary>
    public bool WidgetClockAlwaysVisible { get; set; } = true;
    public bool WidgetServerAlwaysVisible { get; set; } = true;
    public bool WidgetServerAlwaysShowPower { get; set; } = true;
    public bool WidgetServerAlwaysShowRestart { get; set; } = true;
    public bool WidgetServerAlwaysShowStopMacros { get; set; } = true;

    /// <summary>Boîtes mail affichées comme une page du widget (lecture seule) — voir MailFetchService.</summary>
    public List<MailAccount> MailAccounts { get; set; } = new();

    /// <summary>
    /// OBSOLÈTE depuis les espaces de travail (<see cref="WidgetWorkspaces"/>) : gardé uniquement pour migrer une
    /// configuration écrite par une version antérieure (v1.30.0 et avant) vers un espace de travail équivalent au
    /// premier lancement (voir <c>BuilderConfigService.EnsureWidgetWorkspace</c>). Plus lu ni écrit après ça.
    /// </summary>
    public List<string> WidgetPageOrder { get; set; } = new() { "clock", "server", "mail", "pins" };

    /// <summary>
    /// Espaces de travail du widget — chacun un groupe nommé de pages (voir <see cref="WidgetWorkspace"/>), dans le
    /// même esprit que les espaces/écrans d'accueil de l'application mobile. Toujours au moins un espace en pratique :
    /// <c>BuilderConfigService</c> en garantit un (migré depuis l'ancien <see cref="WidgetPageOrder"/> au premier
    /// lancement après mise à jour) si cette liste est vide.
    /// </summary>
    public List<WidgetWorkspace> WidgetWorkspaces { get; set; } = new();

    /// <summary>
    /// Id de l'espace actuellement AFFICHÉ sur le widget (carrousel normal) — un seul à la fois, changé via le
    /// sélecteur du widget parmi les espaces marqués <see cref="WidgetWorkspace.Active"/> (voir ce dernier pour ce
    /// que "actif" veut dire depuis v1.34.12 — un choix multiple, sans rapport direct avec CET id précis).
    /// </summary>
    public string ActiveWidgetWorkspaceId { get; set; } = "";

    /// <summary>
    /// Bibliothèque de listes de tâches (v1.34.8, onglet « Tâches ») — comme les scripts, rangeables dans des
    /// dossiers (<see cref="TodoListInfo.Folder"/>) et choisies par id dans un bloc "todo" du widget (voir
    /// <see cref="WidgetPageBlock.TodoListId"/>) plutôt que d'être embarquées directement dans la page.
    /// </summary>
    public List<TodoListInfo> TodoLists { get; set; } = new();

    /// <summary>Fréquence (en minutes) à laquelle le widget interroge automatiquement les boîtes mail configurées, sans avoir à le fermer/rouvrir.</summary>
    public int WidgetMailRefreshMinutes { get; set; } = 5;

    /// <summary>Messages dont l'envoi est programmé pour plus tard (v1.34.1, voir <see cref="ScheduledEmail"/>) — écrits et relus par le widget (<c>Widget.razor</c>), pas par un service séparé.</summary>
    public List<ScheduledEmail> ScheduledEmails { get; set; } = new();

    /// <summary>
    /// v1.34.15, demandé explicitement ("un onglet historique qui affiche les anciennes notifications") : journal des
    /// notifications déjà apparues dans la cloche du widget (rappel de tâche déclenché, envoi programmé réussi/échoué)
    /// — la cloche elle-même (<c>DueTodoReminders</c>/<c>FailedScheduledEmails</c> dans Widget.razor) ne montre que
    /// celles encore "actives" (pas faites/pas renvoyées) ; celui-ci garde une trace même une fois traitées. Rempli
    /// par <c>CheckTodoNotifications</c>/<c>AttemptScheduledSend</c>, le plus récent en tête, plafonné à
    /// <see cref="NotificationHistoryMaxEntries"/> pour ne pas grossir indéfiniment.
    /// </summary>
    public List<NotificationHistoryEntry> NotificationHistory { get; set; } = new();

    // ----- Page de veille (v1.34.5) : affichée à la place du carrousel normal quand la souris ne survole plus le
    // widget depuis un moment — voir Widget.razor (_idleTimer, _isIdle) et WidgetConfigPanel.razor pour le réglage.
    // Depuis v1.34.6 : QUELLES pages participent à la veille se choisit PAR PAGE (WidgetPageEntry.ShowInIdle/IdleOnly,
    // plusieurs possibles à la fois, en rotation) plutôt qu'une seule page désignée globalement (WidgetIdlePageId,
    // retiré) — seuls le déclenchement (activée/non) et son minutage restent des réglages globaux. -----

    public bool WidgetIdleEnabled { get; set; }

    /// <summary>Secondes sans survol avant de basculer sur la page de veille.</summary>
    public int WidgetIdleDelaySeconds { get; set; } = 30;

    /// <summary>Durée du fondu (en millisecondes) entre le carrousel normal et la page de veille, dans les deux sens.</summary>
    public int WidgetIdleTransitionMs { get; set; } = 600;

    /// <summary>Secondes entre deux pages quand PLUSIEURS pages de l'espace actif ont <see cref="WidgetPageEntry.ShowInIdle"/> activé (v1.34.6) — sans effet s'il n'y en a qu'une (ou zéro).</summary>
    public int WidgetIdleRotateSeconds { get; set; } = 10;

    /// <summary>
    /// v1.34.13, obsolète depuis la v1.34.21 (« plusieurs téléphones », voir <see cref="PhoneTargets"/>) : ancien
    /// champ à téléphone unique, conservé UNIQUEMENT pour désérialiser une config écrite par une version antérieure
    /// — <see cref="MigrateLegacyPhoneTarget"/> le convertit en première entrée de <see cref="PhoneTargets"/> au
    /// chargement, puis le vide. Ne plus utiliser ailleurs dans le code.
    /// </summary>
    public PhoneTarget? PhoneTarget { get; set; }

    /// <summary>
    /// v1.34.21, demandé explicitement ("ajouté plusieurs téléphone") : téléphones joignables pour le bloc
    /// « Téléphone » d'un script (voir StepKind.Phone/ScriptStep.PhoneId) — plusieurs possibles, chacun avec son
    /// nom/adresse IP/port/jeton, même principe que les PC pilotés (<see cref="DeckConfig.Pcs"/>) côté téléphone.
    /// </summary>
    public List<PhoneTarget> PhoneTargets { get; set; } = new();

    /// <summary>Téléphone utilisé par une étape qui n'en désigne pas (<see cref="ScriptStep.PhoneId"/> null) — même principe que <see cref="DeckConfig.DefaultPcId"/> côté PC.</summary>
    public Guid? DefaultPhoneId { get; set; }

    /// <summary>Le téléphone par défaut, ou le premier de la liste si celui désigné a été supprimé, ou null si aucun n'est configuré.</summary>
    [JsonIgnore]
    public PhoneTarget? DefaultPhone => PhoneTargets.FirstOrDefault(p => p.Id == DefaultPhoneId) ?? PhoneTargets.FirstOrDefault();

    /// <summary>
    /// v1.34.16, demandé explicitement ("créer des config tel depuis le pc" + "plusieurs template") : modèles de
    /// configuration téléphone composés depuis le PC (onglet « Téléphone », voir PhoneTemplates.razor) — chacun une
    /// copie complète et indépendante de ce qu'on trouve sur le téléphone (espaces, pages, boutons avec leurs
    /// images), éditée avec les MÊMES composants que l'app téléphone (StreamDeck.Shared/Components/Editor), puis
    /// envoyée à un téléphone en réseau direct via <see cref="PhoneTarget"/> (voir PhoneConfigTransferService).
    /// </summary>
    public List<PhoneConfigTemplate> PhoneTemplates { get; set; } = new();

    /// <summary>
    /// v1.34.17, demandé explicitement ("ajoute le moyen d'exporter depuis le téléphone... un bouton envoyer au
    /// PC") : petit serveur qui tourne DANS Script Builder lui-même (voir PcCommandServerService) et reçoit la
    /// configuration que le téléphone envoie (PcTargetSection côté app téléphone) — complète le sens PC → téléphone
    /// de la v1.34.16. Chaque envoi reçu devient un nouveau modèle dans <see cref="PhoneTemplates"/> (pas d'écrasement
    /// automatique : plus sûr, et ça permet de comparer avant de choisir lequel garder/renvoyer).
    /// </summary>
    public PcServerSettings PcServer { get; set; } = new();

    /// <summary>
    /// v1.35, demandé explicitement ("autorisation de trigger ces bouton par la voix depuis le pc") : réglages de
    /// l'écoute vocale CONTINUE du widget (mot de réveil puis déclencheur d'une touche épinglée — voir
    /// <see cref="WidgetPin.VoiceTriggers"/>, <see cref="IPcVoiceListener"/>, Widget.razor). Sans rapport avec
    /// <see cref="IPcSpeechRecognizer"/>, qui n'enregistre qu'une seule phrase pour remplir un champ de déclencheur.
    /// </summary>
    public WidgetVoiceSettings WidgetVoice { get; set; } = new();

    /// <summary>
    /// v1.37.2, onglet « Calendrier » : créneaux du calendrier (ponctuels ou récurrents chaque semaine), créés depuis
    /// la modale d'un jour — voir <see cref="CalendarEntry"/> et <see cref="CalendarService"/> pour leur déclenchement
    /// (alarme ou script). Remplace <see cref="LegacyCalendarRoutines"/>/<see cref="LegacyCalendarTasks"/> ci-dessous.
    /// </summary>
    public List<CalendarEntry> CalendarEntries { get; set; } = new();

    /// <summary>OBSOLÈTE depuis v1.37.2 — gardé uniquement pour désérialiser une config v1.37.0/1.37.1 (même nom de
    /// propriété JSON, "CalendarTasks") et la migrer vers <see cref="CalendarEntries"/> (voir
    /// <c>MigrateLegacyCalendarRoutinesAndTasks</c>). Toujours vide ensuite.</summary>
    [JsonPropertyName("CalendarTasks")]
    public List<LegacyCalendarTask> LegacyCalendarTasks { get; set; } = new();

    /// <summary>OBSOLÈTE depuis v1.37.2 — même migration que ci-dessus (nom de propriété JSON "CalendarRoutines").</summary>
    [JsonPropertyName("CalendarRoutines")]
    public List<LegacyCalendarRoutine> LegacyCalendarRoutines { get; set; } = new();

    /// <summary>
    /// v1.37, demandé explicitement ("pouvoir mettre des pièce jointe dans les jour du calendrier et des rappel
    /// réveille mettable sur certain") : notes/pièces jointes/réveil propres à un jour précis — voir
    /// <see cref="CalendarDayNote"/>.
    /// </summary>
    public List<CalendarDayNote> CalendarDayNotes { get; set; } = new();

    /// <summary>
    /// v1.38 : roues de WheelConvert (roue radiale Windows) — voir <see cref="WheelConvertWheel"/> et
    /// <see cref="WheelBridgeService"/>, qui les publie automatiquement à chaque modification.
    /// </summary>
    public List<WheelConvertWheel> WheelConvertWheels { get; set; } = new();
}

/// <summary>Réglages de l'écoute vocale continue du widget (voir <see cref="BuilderConfig.WidgetVoice"/>).</summary>
public class WidgetVoiceSettings
{
    public bool Enabled { get; set; }

    /// <summary>Phrase à prononcer avant un déclencheur de touche (sauf touches avec <see cref="WidgetPin.VoiceRequiresWakeWord"/> à faux).</summary>
    public string WakeWord { get; set; } = "ordinateur";

    /// <summary>Durée (secondes) pendant laquelle le déclencheur n'a pas besoin de redire le mot de réveil, après l'avoir entendu une fois.</summary>
    public int AwakeSeconds { get; set; } = 6;
}

/// <summary>Réglages du petit serveur du PC qui reçoit une configuration envoyée par le téléphone (voir <see cref="BuilderConfig.PcServer"/>).</summary>
public class PcServerSettings
{
    public bool Enabled { get; set; }

    /// <summary>Port TCP local d'écoute — à saisir côté téléphone (Réglages → Envoyer au PC) avec l'adresse IP de ce PC.</summary>
    public int Port { get; set; } = 8767;

    /// <summary>Généré à l'activation — à recopier côté téléphone pour autoriser son envoi.</summary>
    public string Token { get; set; } = "";
}

/// <summary>Un modèle de configuration téléphone composé sur le PC (voir <see cref="BuilderConfig.PhoneTemplates"/>).</summary>
public class PhoneConfigTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Nouveau modèle";

    /// <summary>Même modèle que la configuration réelle du téléphone (espaces/pages/boutons) — voir StreamDeck.Models.DeckConfig.</summary>
    public DeckConfig Config { get; set; } = new();

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>Un téléphone joignable pour le bloc « Téléphone » d'un script (voir <see cref="BuilderConfig.PhoneTargets"/>).</summary>
public class PhoneTarget
{
    /// <summary>v1.34.21 : identifiant stable, pour qu'une étape de script puisse viser CE téléphone précisément (voir <see cref="ScriptStep.PhoneId"/>) même si son nom ou son adresse changent ensuite.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>v1.34.21 : nom affiché (Réglages → Téléphone, et le sélecteur de l'étape « Téléphone »).</summary>
    public string Name { get; set; } = "Téléphone";

    public string IpAddress { get; set; } = "";
    public int Port { get; set; } = 8766;
    public string Token { get; set; } = "";
}

/// <summary>Une entrée de l'historique des notifications du widget (v1.34.15, voir <see cref="BuilderConfig.NotificationHistory"/>).</summary>
public class NotificationHistoryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>"todo" (rappel de tâche déclenché) ou "mail-sent"/"mail-failed" (envoi programmé).</summary>
    public string Kind { get; set; } = "";

    public string Text { get; set; } = "";
    public string Meta { get; set; } = "";
    public DateTime At { get; set; } = DateTime.Now;
}

public class StoredWorkspace
{
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Color { get; set; } = "";
    public int Order { get; set; }
}

public class BuilderConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path;

    public BuilderConfig Current { get; private set; } = new();

    public event Action? Changed;

    public BuilderConfigService(IBuilderPlatform platform)
    {
        Directory.CreateDirectory(platform.ConfigDirectory);
        _path = Path.Combine(platform.ConfigDirectory, "config.json");

        try
        {
            if (File.Exists(_path))
                Current = JsonSerializer.Deserialize<BuilderConfig>(File.ReadAllText(_path)) ?? new BuilderConfig();
        }
        catch (Exception)
        {
            // Config illisible : on repart de zéro.
        }

        EnsureWidgetWorkspace();
        MigrateLegacyDisplayMode();
        MigrateLegacyPageKinds();
        MigrateLegacyAlwaysVisible();
        MigrateLegacyPinDimensions();
        MigrateLegacyPageFlagsToBlocks();
        MigrateEmbeddedTodosToLibrary();
        MigrateLegacyPhoneTarget();
        MigrateLegacyCalendarRoutinesAndTasks();
    }

    /// <summary>
    /// v1.34.21 (« plusieurs téléphones ») : convertit l'ancien <see cref="BuilderConfig.PhoneTarget"/> (un seul
    /// téléphone, v1.34.13 à v1.34.20) en première entrée de <see cref="BuilderConfig.PhoneTargets"/>, désignée par
    /// défaut — puis vide l'ancien champ, comme les autres migrations de cette classe (tourne à vide une fois fait).
    /// </summary>
    private void MigrateLegacyPhoneTarget()
    {
        if (Current.PhoneTarget is null) return;

        var migrated = new PhoneTarget
        {
            Name = "Téléphone",
            IpAddress = Current.PhoneTarget.IpAddress,
            Port = Current.PhoneTarget.Port,
            Token = Current.PhoneTarget.Token
        };
        Current.PhoneTargets.Add(migrated);
        Current.DefaultPhoneId = migrated.Id;
        Current.PhoneTarget = null;
    }

    /// <summary>
    /// v1.37.2, demandé explicitement ("routine de la semaine et tache frequente ne doivent pas etre des zone mais
    /// plutot une option lorsque qu'on click sur un jour du calendrier") : fusionne les anciennes « routines »
    /// (v1.37.0, <see cref="LegacyCalendarRoutine"/>) et « tâches fréquentes » (<see cref="LegacyCalendarTask"/>) en
    /// <see cref="CalendarEntry"/> — une routine devient un créneau récurrent équivalent ; une tâche devient un
    /// créneau ponctuel de 30 minutes démarrant à son heure (pas de fin propre dans l'ancien modèle). Vide les deux
    /// anciennes listes juste après, même logique "migration à vide" que les autres méthodes de cette classe.
    /// </summary>
    private void MigrateLegacyCalendarRoutinesAndTasks()
    {
        foreach (var r in Current.LegacyCalendarRoutines)
        {
            Current.CalendarEntries.Add(new CalendarEntry
            {
                Label = r.Label, Color = r.Color, Recurring = true, Day = r.Day, Start = r.Start, End = r.End,
                NotifyAtStart = r.NotifyAtStart, StartSoundKind = r.StartSoundKind, StartSoundScript = r.StartSoundScript,
                NotifyAtEnd = r.NotifyAtEnd, EndSoundKind = r.EndSoundKind, EndSoundScript = r.EndSoundScript,
                LastFiredStartDate = r.LastFiredStartDate, LastFiredEndDate = r.LastFiredEndDate
            });
        }
        Current.LegacyCalendarRoutines.Clear();

        foreach (var t in Current.LegacyCalendarTasks)
        {
            if (t.Done) continue; // tâche "une fois" déjà faite : rien à reporter dans le nouveau modèle
            Current.CalendarEntries.Add(new CalendarEntry
            {
                Label = t.Title, Recurring = false, Date = DateOnly.FromDateTime(t.At),
                Start = t.At.TimeOfDay, End = t.At.TimeOfDay.Add(TimeSpan.FromMinutes(30)),
                NotifyAtStart = t.SoundKind != TimerSoundKind.None, StartSoundKind = t.SoundKind, StartSoundScript = t.SoundScript
            });
        }
        Current.LegacyCalendarTasks.Clear();
    }

    /// <summary>
    /// Garantit qu'il existe toujours au moins un espace de travail du widget, et qu'un espace actif est bien
    /// désigné. Migre une configuration écrite par une version antérieure à v1.31.0 (qui n'avait qu'un ordre de pages
    /// global, <see cref="BuilderConfig.WidgetPageOrder"/>) en reconstruisant un espace "Bureau" équivalent — sauf
    /// que le courrier, qui mélangeait toutes les boîtes sur une seule page, est désormais éclaté en une page par
    /// boîte déjà configurée (nouveau comportement demandé, à partir du même ordre). Reste en mémoire tant que rien
    /// n'est explicitement enregistré ensuite (recalculée à l'identique à chaque démarrage si la config sur disque
    /// n'a pas encore d'espace de travail) — sans conséquence puisque le résultat est déterministe.
    /// </summary>
    private void EnsureWidgetWorkspace()
    {
        if (Current.WidgetWorkspaces.Count == 0)
        {
            var pages = new List<WidgetPageEntry>();
            var order = Current.WidgetPageOrder.Count > 0 ? Current.WidgetPageOrder : new List<string> { "clock", "server", "mail", "pins" };
            foreach (var kind in order)
            {
                if (kind == "mail")
                {
                    foreach (var account in Current.MailAccounts)
                        pages.Add(new WidgetPageEntry { Kind = "mail", MailAccountId = account.Id });
                }
                else
                {
                    pages.Add(new WidgetPageEntry { Kind = kind });
                }
            }

            Current.WidgetWorkspaces.Add(new WidgetWorkspace { Name = "Bureau", Pages = pages });
        }

        // v1.34.12 : garantit un espace "affiché" valide ET actif — au premier lancement, si l'espace pointé par
        // ActiveWidgetWorkspaceId a été supprimé entre-temps (config éditée à la main, ou migration), ou s'il a été
        // désactivé (Active=false) depuis, auquel cas il ne doit plus être proposé/affiché.
        if (Current.WidgetWorkspaces.All(w => !w.Active)) Current.WidgetWorkspaces[0].Active = true;
        if (!Current.WidgetWorkspaces.Any(w => w.Id == Current.ActiveWidgetWorkspaceId && w.Active))
            Current.ActiveWidgetWorkspaceId = Current.WidgetWorkspaces.First(w => w.Active).Id;
    }

    /// <summary>
    /// Migre l'ancien réglage global à trois valeurs ("fixed"/"page"/"hidden", v1.31.0 et avant) vers les nouveaux
    /// interrupteurs simples (v1.32.0) — "page" n'a plus de sens propre puisque n'importe quelle page peut désormais
    /// afficher l'horloge/les contrôles indépendamment (voir <see cref="WidgetPageEntry"/>), donc "page" et "hidden"
    /// deviennent tous deux "pas affiché en permanence". Vide l'ancien champ juste après conversion : comme pour
    /// <see cref="EnsureWidgetWorkspace"/>, tourne à vide (sans effet) une fois migré, y compris si rien n'est encore
    /// enregistré sur disque (le champ vide ne redéclenche pas la conversion).
    /// </summary>
    private void MigrateLegacyDisplayMode()
    {
        if (!string.IsNullOrEmpty(Current.WidgetClockDisplay))
        {
            Current.WidgetClockAlwaysVisible = Current.WidgetClockDisplay == "fixed";
            Current.WidgetClockDisplay = "";
        }

        if (!string.IsNullOrEmpty(Current.WidgetServerControlsDisplay))
        {
            Current.WidgetServerAlwaysVisible = Current.WidgetServerControlsDisplay == "fixed";
            Current.WidgetServerControlsDisplay = "";
        }
    }

    /// <summary>
    /// Migre chaque page écrite par une version antérieure à v1.32.0 (un seul type de contenu par page, voir
    /// <see cref="WidgetPageEntry.Kind"/>) vers le nouveau modèle où une page compose librement plusieurs éléments à
    /// la fois. Vide <c>Kind</c> juste après conversion, même logique de migration "à vide" que les deux méthodes
    /// ci-dessus.
    /// </summary>
    private void MigrateLegacyPageKinds()
    {
        foreach (var page in Current.WidgetWorkspaces.SelectMany(w => w.Pages))
        {
            if (string.IsNullOrEmpty(page.Kind)) continue;

            switch (page.Kind)
            {
                case "clock": page.ShowClock = true; break;
                case "server": page.ShowServerControls = true; break;
                case "mail": page.ShowMail = true; break; // MailAccountId déjà porté par la page
                case "pins": page.ShowPins = true; break;
            }
            page.Kind = "";
        }
    }

    /// <summary>
    /// Migre l'ancien affichage fixe "toujours affiché" de l'horloge/des contrôles serveur en en-tête (v1.32.0) vers
    /// une page dédiée du premier espace de travail (v1.33.0 : plus de réglage général — seule une page peut décider
    /// d'afficher l'horloge ou les contrôles serveur). N'ajoute une page que si aucune page existante n'affiche déjà
    /// ce contenu (évite un doublon si une page le fait déjà). Vide les anciens interrupteurs juste après, même
    /// logique "migration à vide, idempotente jusqu'au premier enregistrement" que les méthodes ci-dessus.
    /// </summary>
    private void MigrateLegacyAlwaysVisible()
    {
        var workspace = Current.WidgetWorkspaces.FirstOrDefault();

        if (Current.WidgetClockAlwaysVisible)
        {
            if (workspace is not null && !workspace.Pages.Any(p => p.ShowClock))
                workspace.Pages.Insert(0, new WidgetPageEntry { Name = "Horloge", ShowClock = true });
            Current.WidgetClockAlwaysVisible = false;
        }

        if (Current.WidgetServerAlwaysVisible)
        {
            if (workspace is not null && !workspace.Pages.Any(p => p.ShowServerControls))
            {
                var insertAt = workspace.Pages.Count > 0 && workspace.Pages[0].ShowClock ? 1 : 0;
                workspace.Pages.Insert(insertAt, new WidgetPageEntry
                {
                    Name = "Serveur",
                    ShowServerControls = true,
                    ShowServerPower = Current.WidgetServerAlwaysShowPower,
                    ShowServerRestart = Current.WidgetServerAlwaysShowRestart,
                    ShowServerStopMacros = Current.WidgetServerAlwaysShowStopMacros
                });
            }
            Current.WidgetServerAlwaysVisible = false;
        }
    }

    /// <summary>
    /// Migre l'ancien réglage global unique de colonnes/lignes de touches épinglées (v1.32.0 et avant) vers chaque
    /// page qui affiche des touches (v1.33.0 : disposition propre à chaque page). Ne fait rien si l'ancien réglage
    /// global est encore à sa valeur par défaut (4×2) — dans ce cas, la valeur par défaut de chaque page suffit déjà.
    /// </summary>
    private void MigrateLegacyPinDimensions()
    {
        if (Current.WidgetPinColumns == 4 && Current.WidgetPinRows == 2) return;

        foreach (var page in Current.WidgetWorkspaces.SelectMany(w => w.Pages).Where(p => p.ShowPins))
        {
            page.PinColumns = Current.WidgetPinColumns;
            page.PinRows = Current.WidgetPinRows;
        }
    }

    /// <summary>
    /// Migre les anciens booléens "un seul bloc par type" d'une page (v1.33.0 et avant : <see cref="WidgetPageEntry.ShowClock"/>
    /// etc.) vers le nouveau modèle de blocs ordonnés, répétables (v1.34.0, <see cref="WidgetPageEntry.Blocks"/>) —
    /// demandé explicitement (pouvoir empiler plusieurs boîtes mail ou plusieurs groupes de touches sur une même page).
    /// Le bloc "touches" reçoit la sélection de TOUTES les touches non masquées, pour que l'affichage reste identique
    /// juste après la migration (l'ancien modèle les affichait toutes automatiquement). Vide les anciens booléens
    /// juste après conversion, même logique "migration à vide" que les autres méthodes ci-dessus.
    /// </summary>
    private void MigrateLegacyPageFlagsToBlocks()
    {
        foreach (var page in Current.WidgetWorkspaces.SelectMany(w => w.Pages))
        {
            if (page.ShowClock)
            {
                page.Blocks.Add(new WidgetPageBlock { Type = "clock" });
                page.ShowClock = false;
            }

            if (page.ShowServerControls)
            {
                page.Blocks.Add(new WidgetPageBlock
                {
                    Type = "server",
                    ShowServerPower = page.ShowServerPower,
                    ShowServerRestart = page.ShowServerRestart,
                    ShowServerStopMacros = page.ShowServerStopMacros
                });
                page.ShowServerControls = false;
            }

            if (page.ShowMail)
            {
                page.Blocks.Add(new WidgetPageBlock { Type = "mail", MailAccountId = page.MailAccountId });
                page.ShowMail = false;
                page.MailAccountId = null;
            }

            if (page.ShowPins)
            {
                page.Blocks.Add(new WidgetPageBlock
                {
                    Type = "pins",
                    PinColumns = page.PinColumns,
                    PinIds = Current.WidgetPins.Where(p => !p.Hidden).Select(p => p.Id).ToList()
                });
                page.ShowPins = false;
            }
        }
    }

    /// <summary>
    /// Migre les tâches autrefois embarquées directement dans un bloc "todo" (<see cref="WidgetPageBlock.Todos"/>,
    /// v1.34.6/v1.34.7) vers une <see cref="TodoListInfo"/> de la bibliothèque (v1.34.8, onglet « Tâches ») — une
    /// nouvelle liste par bloc qui en avait, nommée d'après la page pour rester identifiable, à la racine (pas de
    /// dossier). Le bloc pointe ensuite vers cette liste via <see cref="WidgetPageBlock.TodoListId"/> ; ses tâches
    /// embarquées sont vidées juste après (comme les autres migrations de cette classe).
    /// </summary>
    private void MigrateEmbeddedTodosToLibrary()
    {
        foreach (var page in Current.WidgetWorkspaces.SelectMany(w => w.Pages))
        foreach (var block in page.Blocks.Where(b => b.Type == "todo" && b.Todos.Count > 0))
        {
            var list = new TodoListInfo
            {
                Name = string.IsNullOrWhiteSpace(page.Name) ? "Tâches" : $"Tâches ({page.Name})",
                Items = block.Todos.Select(CloneTodoItem).ToList()
            };
            Current.TodoLists.Add(list);
            block.TodoListId = list.Id;
            block.Todos.Clear();
        }
    }

    /// <summary>Modifie la configuration en gardant tous les autres champs.</summary>
    public void Update(Action<BuilderConfig> change)
    {
        var copy = new BuilderConfig
        {
            ServerFolder = Current.ServerFolder,
            PowerShellPath = Current.PowerShellPath,
            ServerRunAsAdmin = Current.ServerRunAsAdmin,
            LibraryView = Current.LibraryView,
            Collections = new List<string>(Current.Collections),
            ManualFolders = new Dictionary<string, string>(Current.ManualFolders),
            ManualOrder = new Dictionary<string, int>(Current.ManualOrder),
            FolderOrder = new Dictionary<string, int>(Current.FolderOrder),
            DisabledFolders = new HashSet<string>(Current.DisabledFolders, StringComparer.OrdinalIgnoreCase),
            FolderIcon = new Dictionary<string, string>(Current.FolderIcon),
            FolderColor = new Dictionary<string, string>(Current.FolderColor),
            ManualIcon = new Dictionary<string, string>(Current.ManualIcon),
            ManualColor = new Dictionary<string, string>(Current.ManualColor),
            Workspaces = Current.Workspaces.Select(w => new StoredWorkspace { Name = w.Name, Icon = w.Icon, Color = w.Color, Order = w.Order }).ToList(),
            RootWorkspace = new Dictionary<string, string>(Current.RootWorkspace),
            Hotkeys = Current.Hotkeys.Select(h => new HotkeyBinding { Id = h.Id, Enabled = h.Enabled, Action = h.Action, ScriptName = h.ScriptName, Keys = h.Keys }).ToList(),
            QuickAddShortcuts = Current.QuickAddShortcuts.Select(q => new QuickAddBinding { Id = q.Id, Enabled = q.Enabled, Kind = q.Kind, Keys = q.Keys }).ToList(),
            WidgetX = Current.WidgetX,
            WidgetY = Current.WidgetY,
            WidgetAlwaysOnTop = Current.WidgetAlwaysOnTop,
            WidgetVariant = Current.WidgetVariant,
            WidgetFont = Current.WidgetFont,
            WidgetCustomFontPath = Current.WidgetCustomFontPath,
            WidgetRoundedCorners = Current.WidgetRoundedCorners,
            WidgetCustomBackground = Current.WidgetCustomBackground,
            WidgetCustomForeground = Current.WidgetCustomForeground,
            WidgetSizePreset = Current.WidgetSizePreset,
            WidgetFreeResize = Current.WidgetFreeResize,
            WidgetCustomWidth = Current.WidgetCustomWidth,
            WidgetCustomHeight = Current.WidgetCustomHeight,
            WidgetPins = Current.WidgetPins.Select(p => new WidgetPin
            {
                Id = p.Id, ScriptName = p.ScriptName, Icon = p.Icon, Color = p.Color, ImageUrl = p.ImageUrl, ImageOnly = p.ImageOnly, Hidden = p.Hidden,
                Kind = p.Kind,
                Options = p.Options.Select(o => new ChoiceOption { Id = o.Id, Label = o.Label, Icon = o.Icon, ImageUrl = o.ImageUrl, Color = o.Color, Type = o.Type, Script = o.Script }).ToList(),
                VoiceTriggers = new List<string>(p.VoiceTriggers),
                VoiceRequiresWakeWord = p.VoiceRequiresWakeWord,
                // v1.36 : minuteur/chronomètre — voir TimerConfigEditor/WidgetTimerService.
                Timer = p.Timer.Clone(),
                AttachedTimer = p.AttachedTimer?.Clone()
            }).ToList(),
            WidgetPinColumns = Current.WidgetPinColumns,
            WidgetPinRows = Current.WidgetPinRows,
            WidgetClockDisplay = Current.WidgetClockDisplay,
            WidgetServerControlsDisplay = Current.WidgetServerControlsDisplay,
            WidgetClockAlwaysVisible = Current.WidgetClockAlwaysVisible,
            WidgetServerAlwaysVisible = Current.WidgetServerAlwaysVisible,
            WidgetServerAlwaysShowPower = Current.WidgetServerAlwaysShowPower,
            WidgetServerAlwaysShowRestart = Current.WidgetServerAlwaysShowRestart,
            WidgetServerAlwaysShowStopMacros = Current.WidgetServerAlwaysShowStopMacros,
            MailAccounts = Current.MailAccounts.Select(a => new MailAccount
            {
                Id = a.Id, DisplayName = a.DisplayName, ImapHost = a.ImapHost, ImapPort = a.ImapPort,
                UseSsl = a.UseSsl, Username = a.Username, ProtectedPassword = a.ProtectedPassword, Enabled = a.Enabled,
                SmtpEnabled = a.SmtpEnabled, SmtpHost = a.SmtpHost, SmtpPort = a.SmtpPort,
                SignatureEnabled = a.SignatureEnabled, SignatureLogoUrl = a.SignatureLogoUrl, SignatureText = a.SignatureText
            }).ToList(),
            WidgetPageOrder = new List<string>(Current.WidgetPageOrder),
            ActiveWidgetWorkspaceId = Current.ActiveWidgetWorkspaceId,
            WidgetWorkspaces = Current.WidgetWorkspaces.Select(w => new WidgetWorkspace
            {
                Id = w.Id,
                Name = w.Name,
                Active = w.Active,
                Pages = w.Pages.Select(p => new WidgetPageEntry
                {
                    Id = p.Id, Name = p.Name, Kind = p.Kind,
                    Blocks = p.Blocks.Select(b => new WidgetPageBlock
                    {
                        Id = b.Id, Type = b.Type,
                        ShowServerPower = b.ShowServerPower, ShowServerRestart = b.ShowServerRestart, ShowServerStopMacros = b.ShowServerStopMacros,
                        MailAccountId = b.MailAccountId, IdleShowFullMail = b.IdleShowFullMail,
                        PinIds = new List<string>(b.PinIds), PinColumns = b.PinColumns,
                        WeatherLocationName = b.WeatherLocationName, WeatherLat = b.WeatherLat, WeatherLon = b.WeatherLon,
                        WorldClockLabel = b.WorldClockLabel, WorldClockTimeZoneId = b.WorldClockTimeZoneId,
                        WorldClockLat = b.WorldClockLat, WorldClockLon = b.WorldClockLon,
                        TodoListId = b.TodoListId,
                        Todos = b.Todos.Select(CloneTodoItem).ToList(),
                        // v1.37.2 : jour affiché par un bloc "calendar" — voir WidgetPageBlock.
                        CalendarDayMode = b.CalendarDayMode, CalendarDayOffset = b.CalendarDayOffset, CalendarSpecificDate = b.CalendarSpecificDate,
                        CalendarShowManageButton = b.CalendarShowManageButton, CalendarFocusMode = b.CalendarFocusMode
                    }).ToList(),
                    ShowClock = p.ShowClock,
                    ShowServerControls = p.ShowServerControls, ShowServerPower = p.ShowServerPower, ShowServerRestart = p.ShowServerRestart, ShowServerStopMacros = p.ShowServerStopMacros,
                    ShowMail = p.ShowMail, MailAccountId = p.MailAccountId,
                    ShowPins = p.ShowPins, PinColumns = p.PinColumns, PinRows = p.PinRows,
                    ShowInIdle = p.ShowInIdle, IdleOnly = p.IdleOnly
                }).ToList()
            }).ToList(),
            TodoLists = Current.TodoLists.Select(l => new TodoListInfo
            {
                Id = l.Id, Name = l.Name, Folder = l.Folder, Items = l.Items.Select(CloneTodoItem).ToList()
            }).ToList(),
            WidgetMailRefreshMinutes = Current.WidgetMailRefreshMinutes,
            ScheduledEmails = Current.ScheduledEmails.Select(s => new ScheduledEmail
            {
                Id = s.Id, AccountId = s.AccountId, To = s.To, Subject = s.Subject, Body = s.Body,
                InReplyToMessageId = s.InReplyToMessageId, ScheduledAt = s.ScheduledAt, Status = s.Status,
                SentAt = s.SentAt, LastError = s.LastError, Attempts = s.Attempts
            }).ToList(),
            WidgetIdleEnabled = Current.WidgetIdleEnabled,
            WidgetIdleDelaySeconds = Current.WidgetIdleDelaySeconds,
            WidgetIdleTransitionMs = Current.WidgetIdleTransitionMs,
            WidgetIdleRotateSeconds = Current.WidgetIdleRotateSeconds,
            PhoneTargets = Current.PhoneTargets.Select(p => new PhoneTarget
            {
                Id = p.Id, Name = p.Name, IpAddress = p.IpAddress, Port = p.Port, Token = p.Token
            }).ToList(),
            DefaultPhoneId = Current.DefaultPhoneId,
            NotificationHistory = Current.NotificationHistory.Select(n => new NotificationHistoryEntry
            {
                Id = n.Id, Kind = n.Kind, Text = n.Text, Meta = n.Meta, At = n.At
            }).ToList(),
            PhoneTemplates = Current.PhoneTemplates.Select(t => new PhoneConfigTemplate
            {
                Id = t.Id, Name = t.Name, Config = t.Config.Clone(), UpdatedAt = t.UpdatedAt
            }).ToList(),
            PcServer = new PcServerSettings
            {
                Enabled = Current.PcServer.Enabled, Port = Current.PcServer.Port, Token = Current.PcServer.Token
            },
            WidgetVoice = new WidgetVoiceSettings
            {
                Enabled = Current.WidgetVoice.Enabled, WakeWord = Current.WidgetVoice.WakeWord, AwakeSeconds = Current.WidgetVoice.AwakeSeconds
            },
            // v1.37 : onglet « Calendrier » — voir CalendarModels.cs.
            // v1.37.2 : LegacyCalendarTasks/LegacyCalendarRoutines ne sont jamais re-remplies après la migration au
            // chargement (voir MigrateLegacyCalendarRoutinesAndTasks) — inutile de les recopier ici, elles restent vides.
            CalendarEntries = Current.CalendarEntries.Select(e => e.Clone()).ToList(),
            CalendarDayNotes = Current.CalendarDayNotes.Select(d => d.Clone()).ToList(),
            // v1.38 : roues WheelConvert — voir WheelConvertModels.cs.
            WheelConvertWheels = Current.WheelConvertWheels.Select(w => w.Clone()).ToList()
        };
        change(copy);
        if (copy.WidgetWorkspaces.Count == 0) copy.WidgetWorkspaces.Add(new WidgetWorkspace { Name = "Bureau" });
        // v1.34.12 : filet de sécurité générique ici (plutôt que dans chaque endroit qui peut désactiver/supprimer un
        // espace) pour garantir : au moins un espace actif (sinon le sélecteur du widget se retrouverait vide), ET
        // ActiveWidgetWorkspaceId qui pointe toujours vers un espace qui existe encore ET qui est actif — sinon
        // bascule sur le premier espace actif trouvé.
        if (copy.WidgetWorkspaces.Count > 0 && copy.WidgetWorkspaces.All(w => !w.Active))
            copy.WidgetWorkspaces[0].Active = true;
        if (copy.WidgetWorkspaces.Count > 0 && !copy.WidgetWorkspaces.Any(w => w.Id == copy.ActiveWidgetWorkspaceId && w.Active))
            copy.ActiveWidgetWorkspaceId = copy.WidgetWorkspaces.First(w => w.Active).Id;
        // v1.34.21 : même filet de sécurité pour le téléphone par défaut — s'il a été supprimé, se rabat sur le
        // premier restant (ou null s'il n'en reste aucun).
        if (copy.DefaultPhoneId is null || !copy.PhoneTargets.Any(p => p.Id == copy.DefaultPhoneId))
            copy.DefaultPhoneId = copy.PhoneTargets.FirstOrDefault()?.Id;
        Save(copy);
    }

    /// <summary>Copie profonde d'une <see cref="TodoItem"/> — utilisée à la fois pour <see cref="TodoListInfo.Items"/> (v1.34.8) et pour l'ancien <see cref="WidgetPageBlock.Todos"/> embarqué (migration seulement).</summary>
    private static TodoItem CloneTodoItem(TodoItem t) => new()
    {
        Id = t.Id, Text = t.Text, Description = t.Description, Done = t.Done, DueAt = t.DueAt,
        Level = t.Level, NotifyDaysBefore = t.NotifyDaysBefore, NotifyRepeat = t.NotifyRepeat,
        NotifyTarget = t.NotifyTarget, LastNotifiedForDueAt = t.LastNotifiedForDueAt, Hidden = t.Hidden
    };

    public void Save(BuilderConfig config)
    {
        config.ServerFolder = (config.ServerFolder ?? string.Empty).Trim().Trim('"');
        config.PowerShellPath = string.IsNullOrWhiteSpace(config.PowerShellPath) ? "powershell.exe" : config.PowerShellPath.Trim().Trim('"');

        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(config, JsonOptions));
        File.Move(tmp, _path, overwrite: true);

        Current = config;
        Changed?.Invoke();
    }
}
