using System.Text.Json;
using System.Text.Json.Serialization;
using StreamDeck.ScriptBuilder.Models;

namespace StreamDeck.ScriptBuilder.Services;

/// <summary>
/// v1.38 — pont vers WheelConvert (roue radiale Windows : Shift + clic droit maintenu, molette pour changer de roue).
/// Publie <see cref="BuilderConfig.WheelConvertWheels"/> dans %AppData%\WheelConvert\providers\streamdeck.json
/// automatiquement, à chaque modification de la configuration ; WheelConvert surveille ce fichier et met ses roues à
/// jour en direct, sans redémarrage. Les deux applications restent indépendantes : si WheelConvert n'est pas installé,
/// le fichier attend simplement son premier lancement.
///
/// <para>Exécution : chaque secteur « script » lance directement le .ps1 de la bibliothèque avec le PowerShell réglé
/// ici (<see cref="BuilderConfig.PowerShellPath"/>) — exactement comme une touche du widget (<c>ScriptTester</c>), pas
/// de serveur ni de jeton à viser. Un script désactivé ou introuvable n'est pas publié.</para>
/// </summary>
public sealed class WheelBridgeService
{
    // ----- Contrat partagé avec WheelConvert (Core/ExternalActions.cs, version 2) -----

    private sealed class RunDto
    {
        public string Type { get; set; } = "powershell";
        public string Target { get; set; } = "";
        public string? Shell { get; set; }
    }

    private sealed class ActionDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Icon { get; set; }
        public string? Color { get; set; }
        public RunDto Run { get; set; } = new();
    }

    private sealed class WheelDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Icon { get; set; }
        public string? Color { get; set; }
        public List<ActionDto> Actions { get; set; } = new();
    }

    private sealed class ProviderDto
    {
        public string Provider { get; set; } = "Stream Deck";
        public int Version { get; set; } = 2;
        public List<WheelDto> Wheels { get; set; } = new();
    }

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly BuilderConfigService _config;
    private readonly ScriptStore _store;
    private readonly object _gate = new();
    private string? _lastPayload;

    /// <summary>Dernière erreur d'écriture (affichée dans Réglages), null si tout va bien.</summary>
    public string? LastError { get; private set; }

    /// <summary>Déclenché après chaque publication (pour rafraîchir l'état dans Réglages).</summary>
    public event Action? Published;

    public WheelBridgeService(BuilderConfigService config, ScriptStore store)
    {
        _config = config;
        _store = store;
        _config.Changed += Publish;
        Publish();
        WatchWheelConvert();
    }

    public static string WheelFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WheelConvert");

    public static string FilePath => Path.Combine(WheelFolder, "providers", "streamdeck.json");

    /// <summary>WheelConvert a-t-il déjà été lancé sur ce PC (son dossier de réglages existe) ?</summary>
    public static bool WheelConvertInstalled => File.Exists(Path.Combine(WheelFolder, "settings.json"));

    // ================= Roues propres à WheelConvert (lecture seule ici) =================

    /// <summary>Un secteur d'une roue créée dans WheelConvert lui-même (designer WheelConvert).</summary>
    public sealed record LocalItem(string Name, string Glyph, string Color, bool IsLink);

    /// <summary>Une roue créée dans WheelConvert lui-même — affichée dans l'onglet Roues, modifiable seulement dans WheelConvert.</summary>
    public sealed record LocalWheel(string Id, string Name, string Glyph, string Color, IReadOnlyList<LocalItem> Items);

    private FileSystemWatcher? _wheelConvertWatcher;
    private Timer? _wheelConvertDebounce;

    /// <summary>Déclenché quand les réglages de WheelConvert changent (ses propres roues, l'affichage des roues Stream Deck).</summary>
    public event Action? WheelConvertChanged;

    /// <summary>Roues créées dans WheelConvert (lues dans son settings.json), dans l'ordre où il les affiche.</summary>
    public IReadOnlyList<LocalWheel> ReadWheelConvertWheels()
    {
        var list = new List<LocalWheel>();
        try
        {
            using var doc = JsonDocument.Parse(ReadShared(Path.Combine(WheelFolder, "settings.json")));
            if (!TryGet(doc.RootElement, "Wheels", out var wheels) || wheels.ValueKind != JsonValueKind.Array || wheels.GetArrayLength() == 0)
            {
                // Ancien format de WheelConvert (une seule roue, pas encore relancé depuis la mise à jour)
                if (TryGet(doc.RootElement, "QuickActions", out var legacy) && legacy.ValueKind == JsonValueKind.Array)
                {
                    var legacyItems = legacy.EnumerateArray()
                        .Select(a => new LocalItem(Str(a, "Name", "Action"), Str(a, "Glyph", "⭐"), Str(a, "Color", "#2D7DFF"), false))
                        .ToList();
                    list.Add(new LocalWheel("legacy", "Principale", "🏠", "#2D7DFF", legacyItems));
                }
                return list;
            }

            foreach (var w in wheels.EnumerateArray())
            {
                var items = new List<LocalItem>();
                if (TryGet(w, "Actions", out var actions) && actions.ValueKind == JsonValueKind.Array)
                {
                    foreach (var a in actions.EnumerateArray())
                        items.Add(new LocalItem(Str(a, "Name", "Action"), Str(a, "Glyph", "⭐"), Str(a, "Color", "#2D7DFF"),
                            Str(a, "Type", "") == "OpenWheel"));
                }
                list.Add(new LocalWheel(Str(w, "Id", Guid.NewGuid().ToString("N")), Str(w, "Name", "Roue"),
                    Str(w, "Glyph", "⭐"), Str(w, "Color", "#2D7DFF"), items));
            }
        }
        catch { /* WheelConvert absent ou fichier en cours d'écriture : liste vide */ }
        return list;
    }

    /// <summary>Faux si l'option « Afficher les roues envoyées par Stream Deck » est décochée dans WheelConvert.</summary>
    public bool WheelConvertShowsStreamDeck()
    {
        try
        {
            using var doc = JsonDocument.Parse(ReadShared(Path.Combine(WheelFolder, "settings.json")));
            return !TryGet(doc.RootElement, "ShowExternalActions", out var v) || v.ValueKind != JsonValueKind.False;
        }
        catch { return true; }
    }

    /// <summary>Chemin de WheelConvert.exe (écrit par WheelConvert à chaque lancement), null s'il n'a jamais tourné.</summary>
    public static string? WheelConvertExe
    {
        get
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(WheelFolder, "app.json")));
                string? exe = TryGet(doc.RootElement, "exePath", out var v) ? v.GetString() : null;
                return exe is not null && File.Exists(exe) ? exe : null;
            }
            catch { return null; }
        }
    }

    /// <summary>Ouvre le designer de WheelConvert (le lance s'il ne tourne pas, sinon ramène sa fenêtre).</summary>
    public bool OpenWheelConvertDesigner()
    {
        var exe = WheelConvertExe;
        if (exe is null) return false;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, "--designer") { UseShellExecute = true });
            return true;
        }
        catch { return false; }
    }

    private void WatchWheelConvert()
    {
        try
        {
            Directory.CreateDirectory(WheelFolder);
            _wheelConvertDebounce = new Timer(_ => WheelConvertChanged?.Invoke());
            _wheelConvertWatcher = new FileSystemWatcher(WheelFolder, "settings.json")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            FileSystemEventHandler onChange = (_, _) => _wheelConvertDebounce.Change(300, Timeout.Infinite);
            _wheelConvertWatcher.Changed += onChange;
            _wheelConvertWatcher.Created += onChange;
            _wheelConvertWatcher.Deleted += onChange;
            _wheelConvertWatcher.Renamed += (_, _) => _wheelConvertDebounce.Change(300, Timeout.Infinite);
        }
        catch { /* pas de suivi en direct : la liste se relit à chaque ouverture de l'onglet */ }
    }

    private static string ReadShared(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(fs);
        return reader.ReadToEnd();
    }

    private static bool TryGet(JsonElement e, string name, out JsonElement value)
    {
        if (e.ValueKind == JsonValueKind.Object)
            foreach (var p in e.EnumerateObject())
                if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) { value = p.Value; return true; }
        value = default;
        return false;
    }

    private static string Str(JsonElement e, string name, string fallback) =>
        TryGet(e, name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()) ? v.GetString()! : fallback;

    /// <summary>Republie maintenant (ex. après avoir modifié un script hors de l'application).</summary>
    public void Publish()
    {
        try
        {
            string payload = JsonSerializer.Serialize(Build(), Json);
            lock (_gate)
            {
                if (payload == _lastPayload && File.Exists(FilePath)) return; // rien de neuf : pas de réécriture
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, payload);
                File.Move(tmp, FilePath, overwrite: true); // écriture atomique : jamais de fichier à moitié lu
                _lastPayload = payload;
            }
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = ex.Message; // le Stream Deck continue de fonctionner normalement
        }
        Published?.Invoke();
    }

    private ProviderDto Build()
    {
        var cfg = _config.Current;
        var scripts = _store.HasServerFolder
            ? _store.List().GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, ScriptInfo>(StringComparer.OrdinalIgnoreCase);
        var wheels = cfg.WheelConvertWheels;
        string shell = string.IsNullOrWhiteSpace(cfg.PowerShellPath) ? "powershell.exe" : cfg.PowerShellPath;

        var dto = new ProviderDto();
        foreach (var wheel in wheels)
        {
            var w = new WheelDto
            {
                Id = wheel.Id,
                Name = string.IsNullOrWhiteSpace(wheel.Name) ? "Roue" : wheel.Name,
                Icon = NullIfEmpty(wheel.Icon),
                Color = NullIfEmpty(wheel.Color)
            };

            foreach (var item in wheel.Items)
            {
                if (item.Kind == "wheel")
                {
                    var target = wheels.FirstOrDefault(x => x.Id == item.TargetWheelId);
                    if (target is null || target.Id == wheel.Id) continue;
                    w.Actions.Add(new ActionDto
                    {
                        Id = item.Id,
                        Name = Pick(item.Label, target.Name),
                        Icon = Pick(item.Icon, target.Icon),
                        Color = NullIfEmpty(Pick(item.Color, target.Color)),
                        Run = new RunDto { Type = "wheel", Target = target.Id }
                    });
                    continue;
                }

                if (!scripts.TryGetValue(item.ScriptName ?? "", out var info) || !info.Enabled) continue;
                w.Actions.Add(new ActionDto
                {
                    Id = item.Id,
                    Name = Pick(item.Label, info.Id),
                    Icon = NullIfEmpty(Pick(item.Icon, info.Icon, "fa-solid fa-scroll")),
                    Color = NullIfEmpty(Pick(item.Color, info.Color, wheel.Color)),
                    Run = new RunDto { Type = "powershell", Target = info.Path, Shell = shell }
                });
            }

            dto.Wheels.Add(w);
        }
        return dto;
    }

    // ----- Utilitaires pour l'interface -----

    /// <summary>Roues qui contiennent déjà ce script.</summary>
    public IReadOnlyList<WheelConvertWheel> WheelsContaining(string scriptName) =>
        _config.Current.WheelConvertWheels
            .Where(w => w.Items.Any(i => i.Kind == "script" && i.ScriptName.Equals(scriptName, StringComparison.OrdinalIgnoreCase)))
            .ToList();

    /// <summary>Ajoute ce script à la roue, ou l'en retire s'il y est déjà.</summary>
    public void ToggleScript(string wheelId, string scriptName, string? icon = null, string? color = null)
    {
        if (string.IsNullOrWhiteSpace(scriptName)) return;
        _config.Update(c =>
        {
            var wheel = c.WheelConvertWheels.FirstOrDefault(w => w.Id == wheelId);
            if (wheel is null) return;
            int removed = wheel.Items.RemoveAll(i => i.Kind == "script" && i.ScriptName.Equals(scriptName, StringComparison.OrdinalIgnoreCase));
            if (removed == 0)
                wheel.Items.Add(new WheelConvertItem { Kind = "script", ScriptName = scriptName, Icon = icon ?? "", Color = color ?? "" });
        });
    }

    private static string Pick(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
