using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamDeck.Models;

public class DeckConfig
{
    public const int CurrentVersion = 3;
    public const string DefaultServerUrl = "http://192.168.1.100:8765";

    /// <summary>Version du format : sert à migrer les anciennes configs (0 = avant les PC multiples).</summary>
    public int Version { get; set; }

    /// <summary>PC pilotables (chacun avec son serveur, son token et son adresse MAC).</summary>
    public List<PcProfile> Pcs { get; set; } = new();

    /// <summary>PC utilisé par défaut quand un espace n'en désigne pas.</summary>
    public Guid DefaultPcId { get; set; }

    public List<Workspace> Workspaces { get; set; } = new();

    /// <summary>Dernier espace de travail affiché.</summary>
    public Guid ActiveWorkspaceId { get; set; }

    public SecuritySettings Security { get; set; } = new();
    public DisplaySettings Display { get; set; } = new();
    public VoiceSettings Voice { get; set; } = new();
    public PhoneServerSettings PhoneServer { get; set; } = new();

    /// <summary>Cible PC pour « Envoyer au PC » (v1.34.17, voir <see cref="PcPushTarget"/>) — nullable : rien à envoyer tant que ce n'est pas renseigné.</summary>
    public PcPushTarget? PcPushTarget { get; set; }

    // ----- Anciens formats : lus une fois, migrés, jamais réécrits -----

    [JsonPropertyName("ServerUrl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyServerUrl { get; set; }

    [JsonPropertyName("Token")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyToken { get; set; }

    [JsonPropertyName("Columns")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LegacyColumns { get; set; }

    [JsonPropertyName("Buttons")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<DeckButton>? LegacyButtons { get; set; }

    [JsonIgnore]
    public Workspace ActiveWorkspace =>
        Workspaces.FirstOrDefault(w => w.Id == ActiveWorkspaceId) ?? Workspaces[0];

    [JsonIgnore]
    public PcProfile DefaultPc =>
        Pcs.FirstOrDefault(p => p.Id == DefaultPcId) ?? Pcs[0];

    public DeckConfig Clone() =>
        JsonSerializer.Deserialize<DeckConfig>(JsonSerializer.Serialize(this))!;

    /// <summary>PC visé par un espace (le PC par défaut si l'espace n'en désigne pas).</summary>
    public PcProfile PcFor(Workspace workspace) =>
        Pcs.FirstOrDefault(p => p.Id == workspace.PcId) ?? DefaultPc;

    /// <summary>PC visé par un bouton : celui du bouton, sinon celui de l'espace.</summary>
    public PcProfile PcFor(Workspace workspace, DeckButton button) =>
        Pcs.FirstOrDefault(p => p.Id == button.PcId) ?? PcFor(workspace);

    /// <summary>Migre les anciens formats, garantit un état cohérent et remet chaque page en ordre.</summary>
    public void EnsureValid()
    {
        var fromOlder = Version < CurrentVersion;

        MigrateFirstFormat();
        if (fromOlder)
        {
            MigrateWorkspaceGrids();
            MigratePcs();
        }

        Version = CurrentVersion;

        // Ordre : PC d'abord, car les espaces et boutons y font référence.
        if (Pcs.Count == 0)
            Pcs.Add(new PcProfile { Name = "Mon PC", ServerUrl = DefaultServerUrl });
        if (Pcs.All(p => p.Id != DefaultPcId))
            DefaultPcId = Pcs[0].Id;

        if (Workspaces.Count == 0)
            Workspaces.Add(new Workspace { Name = "Principal", Icon = "fa-solid fa-table-cells-large" });

        foreach (var w in Workspaces)
        {
            if (w.PcId is { } id && Pcs.All(p => p.Id != id)) w.PcId = null;
            w.Normalize();

            foreach (var b in w.Pages.SelectMany(p => p.Buttons))
                if (b.PcId is { } bid && Pcs.All(p => p.Id != bid)) b.PcId = null;
        }

        if (Workspaces.All(w => w.Id != ActiveWorkspaceId))
            ActiveWorkspaceId = Workspaces[0].Id;

        Security ??= new SecuritySettings();
        Display ??= new DisplaySettings();
        Display.IdleMinutes = Math.Clamp(Display.IdleMinutes, 0, DisplaySettings.MaxIdleMinutes);
        Voice ??= new VoiceSettings();
        Voice.AwakeSeconds = Math.Clamp(Voice.AwakeSeconds, 1, VoiceSettings.MaxAwakeSeconds);

        PhoneServer ??= new PhoneServerSettings();
        PhoneServer.Port = Math.Clamp(PhoneServer.Port, 1024, 65535);
        // Un jeton vide laisserait la porte ouverte à qui connaît juste l'adresse IP et le port — en générer un
        // automatiquement à la première activation plutôt que de démarrer le serveur sans protection.
        if (PhoneServer.Enabled && string.IsNullOrWhiteSpace(PhoneServer.Token))
            PhoneServer.Token = Guid.NewGuid().ToString("N");
    }

    /// <summary>Premier format (avant les espaces) : Buttons et Columns à la racine.</summary>
    private void MigrateFirstFormat()
    {
        if (LegacyButtons is null && LegacyColumns is null) return;

        if (Workspaces.Count == 0)
        {
            var columns = Math.Clamp(LegacyColumns ?? 4, 1, DeckPage.MaxColumns);
            var count = LegacyButtons?.Count ?? 0;
            var rows = Math.Clamp((int)Math.Ceiling(count / (double)columns), 1, DeckPage.MaxRows);

            var workspace = new Workspace { Name = "Principal", Icon = "fa-solid fa-table-cells-large" };
            workspace.Pages[0].Columns = columns;
            workspace.Pages[0].Rows = rows;
            workspace.Pages[0].Buttons.AddRange(LegacyButtons ?? new List<DeckButton>());
            Workspaces.Add(workspace);
        }

        LegacyButtons = null;
        LegacyColumns = null;
    }

    /// <summary>Deuxième format : la grille était réglée par espace ; elle l'est maintenant par page.</summary>
    private void MigrateWorkspaceGrids()
    {
        foreach (var w in Workspaces)
        {
            if (w.LegacyColumns is null && w.LegacyRows is null) continue;

            foreach (var page in w.Pages)
            {
                if (w.LegacyColumns is { } c) page.Columns = c;
                if (w.LegacyRows is { } r) page.Rows = r;
            }

            w.LegacyColumns = null;
            w.LegacyRows = null;
        }
    }

    /// <summary>Anciens formats : un seul serveur à la racine -> premier PC.</summary>
    private void MigratePcs()
    {
        if (Pcs.Count == 0 && (LegacyServerUrl is not null || LegacyToken is not null))
        {
            Pcs.Add(new PcProfile
            {
                Name = "Mon PC",
                ServerUrl = string.IsNullOrWhiteSpace(LegacyServerUrl) ? DefaultServerUrl : LegacyServerUrl,
                Token = LegacyToken ?? string.Empty
            });
        }

        LegacyServerUrl = null;
        LegacyToken = null;
    }
}

/// <summary>Un PC pilotable : serveur de scripts + adresse MAC pour le réveil.</summary>
public class PcProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Mon PC";
    public string ServerUrl { get; set; } = DeckConfig.DefaultServerUrl;
    public string Token { get; set; } = "";

    /// <summary>Adresse MAC de la carte Ethernet (réveil Wake-on-LAN), ex : 6C-02-E0-3E-35-7E.</summary>
    public string Mac { get; set; } = "";
}

public class SecuritySettings
{
    public const int MinPinLength = 4;
    public const int MaxPinLength = 8;

    public string PinSalt { get; set; } = "";
    public string PinHash { get; set; } = "";

    /// <summary>Demande le code PIN pour ouvrir les réglages.</summary>
    public bool LockSettings { get; set; }

    [JsonIgnore]
    public bool HasPin => !string.IsNullOrEmpty(PinHash);
}

public class DisplaySettings
{
    public const int MaxIdleMinutes = 240;

    /// <summary>Assombrit l'écran après N minutes sans toucher (0 = jamais).</summary>
    public int IdleMinutes { get; set; }

    /// <summary>Vibration au toucher (Android).</summary>
    public bool Haptics { get; set; } = true;
}

/// <summary>Écoute continue (Android) : un mot de réveil ouvre une courte fenêtre pour dire une commande.</summary>
public class VoiceSettings
{
    public const int DefaultAwakeSeconds = 6;
    public const int MaxAwakeSeconds = 30;

    /// <summary>Écoute désactivée par défaut : demande le micro et tourne en tâche de fond, à activer explicitement.</summary>
    public bool Enabled { get; set; }

    /// <summary>Mot ou phrase de réveil (ex : "Cortana"). Vide : aucun bouton avec « nécessite le mot de réveil » ne peut se déclencher.</summary>
    public string WakeWord { get; set; } = "";

    /// <summary>Secondes pendant lesquelles une commande est attendue après le mot de réveil.</summary>
    public int AwakeSeconds { get; set; } = DefaultAwakeSeconds;

    // ----- « Retrouver mon téléphone » à la voix (v1.35, demandé explicitement) : un réglage à part plutôt qu'un
    // bouton à poser sur une page — jusqu'ici, ButtonKind.FindPhone n'était déclenchable à la voix qu'en plaçant un
    // bouton quelque part et en lui donnant des VoiceTriggers (voir VoiceMatcher/DeckButton). Ici, l'app compose un
    // bouton FindPhone "virtuel" (jamais affiché sur aucune page) à partir de ces réglages avant de transmettre à
    // VoiceMatcher — VoiceMatcher lui-même n'a pas besoin de changer. -----

    /// <summary>Active la commande vocale dédiée (indépendante de tout bouton posé sur une page).</summary>
    public bool FindPhoneVoiceEnabled { get; set; }

    /// <summary>Phrase reconnue (ex : "retrouve mon telephone") — comparée normalisée, comme les autres déclencheurs vocaux.</summary>
    public string FindPhoneVoiceTrigger { get; set; } = "retrouve mon telephone";

    /// <summary>Même logique que <see cref="Workspace.DeckButton.VoiceRequiresWakeWord"/> : vrai (par défaut) = seulement après le mot de réveil ; faux = reconnue à tout moment.</summary>
    public bool FindPhoneVoiceRequiresWakeWord { get; set; } = true;

    // ----- Sonneries personnalisées (v1.35, demandé explicitement) : fichiers audio locaux (mp3) ajoutés depuis les
    // Réglages, copiés dans le dossier de l'app (voir IAppPlatform.PickAudioFileAsync) pour rester lisibles même si
    // le fichier d'origine est déplacé/supprimé. -----

    /// <summary>Noms de fichiers (dans le sous-dossier "Ringtones" du dossier de config de l'app) ajoutés comme sonneries possibles.</summary>
    public List<string> CustomRingtoneFiles { get; set; } = new();

    /// <summary>Nom de fichier choisi parmi <see cref="CustomRingtoneFiles"/> pour « Retrouver mon téléphone » — vide/absent de la liste = sonnerie par défaut de l'appareil (comportement d'origine).</summary>
    public string? SelectedRingtoneFile { get; set; }
}

/// <summary>
/// v1.34.13, demandé explicitement ("les script pc direction le tel") : petit serveur local (voir
/// PhoneCommandServerService côté Android) qui reçoit des commandes envoyées par un script PC (nouveau bloc
/// « Téléphone » dans Script Builder) — sonner/alarme (réutilise <see cref="FindPhoneVoiceEnabled">le même mécanisme
/// que « Retrouver le téléphone »</see>) ou ouvrir une application installée. Protégé par un jeton (comme
/// <see cref="PcProfile.Token"/> dans l'autre sens, PC → serveur), généré automatiquement à la première activation.
/// Désactivé par défaut : le téléphone n'écoute sur aucun port tant que ce n'est pas explicitement activé.
/// </summary>
public class PhoneServerSettings
{
    public bool Enabled { get; set; }

    /// <summary>Port TCP local d'écoute — à saisir côté PC (Script Builder, Réglages) avec l'adresse IP du téléphone.</summary>
    public int Port { get; set; } = 8766;

    /// <summary>Généré automatiquement (voir DeckConfig.EnsureValid) — à copier côté PC pour autoriser ses commandes.</summary>
    public string Token { get; set; } = "";
}

/// <summary>
/// v1.34.17, demandé explicitement ("ajoute le moyen d'exporter depuis le téléphone... un bouton envoyer au PC") :
/// adresse/port/jeton du petit serveur qui tourne dans Script Builder (PcCommandServerService, sens inverse de
/// <see cref="PhoneServerSettings"/>) — pour que ce téléphone puisse y envoyer sa configuration (PcTargetSection) en
/// réseau direct, qui devient un nouveau modèle côté PC (onglet « Téléphone »). Saisi à la main (comme
/// <see cref="PcProfile.ServerUrl"/>/<see cref="PcProfile.Token"/>) plutôt que généré ici : c'est le PC qui génère
/// son propre jeton et l'affiche à recopier.
/// </summary>
public class PcPushTarget
{
    public string IpAddress { get; set; } = "";
    public int Port { get; set; } = 8767;
    public string Token { get; set; } = "";
}
