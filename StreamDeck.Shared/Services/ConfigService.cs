using System.Text.Json;
using StreamDeck.Models;

namespace StreamDeck.Services;

public class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _lock = new();

    // Page affichée par espace : conservée pendant l'exécution seulement.
    private readonly Dictionary<Guid, int> _pageIndexes = new();

    public DeckConfig Current { get; private set; }

    /// <summary>Levé après un enregistrement des réglages.</summary>
    public event Action? Changed;

    public ConfigService(IAppPlatform platform)
    {
        Directory.CreateDirectory(platform.ConfigDirectory);
        _path = Path.Combine(platform.ConfigDirectory, "config.json");

        Current = Load();
    }

    private DeckConfig Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var loaded = JsonSerializer.Deserialize<DeckConfig>(File.ReadAllText(_path));
                if (loaded is not null)
                {
                    var migrated = loaded.Version < DeckConfig.CurrentVersion
                                   || loaded.LegacyButtons is not null
                                   || loaded.LegacyColumns is not null;

                    loaded.EnsureValid();

                    // Anciennes configs : icône emoji -> icône Font Awesome par défaut.
                    foreach (var b in loaded.Workspaces.SelectMany(w => w.Pages).SelectMany(p => p.Buttons))
                    {
                        if (!(b.Icon ?? string.Empty).StartsWith("fa-", StringComparison.Ordinal))
                            b.Icon = "fa-solid fa-bolt";
                    }

                    if (migrated) TryPersist(loaded);
                    return loaded;
                }
            }
        }
        catch
        {
            // Config illisible : on repart des valeurs par défaut.
        }

        var config = new DeckConfig();
        var workspace = new Workspace { Name = "Principal", Icon = "fa-solid fa-table-cells-large" };
        workspace.Pages[0].Buttons.Add(new DeckButton
        {
            Label = "Time to shine",
            Script = "timetoshine",
            Icon = "fa-solid fa-wand-magic-sparkles",
            Color = "#f59e0b"
        });
        config.Workspaces.Add(workspace);
        config.EnsureValid();
        return config;
    }

    public void Save(DeckConfig config)
    {
        lock (_lock)
        {
            config.EnsureValid();
            Persist(config);
            Current = config.Clone();
        }

        Changed?.Invoke();
    }

    /// <summary>Change d'espace de travail et s'en souvient au prochain lancement.</summary>
    public void SetActiveWorkspace(Guid id)
    {
        lock (_lock)
        {
            if (Current.Workspaces.All(w => w.Id != id)) return;
            Current.ActiveWorkspaceId = id;
            TryPersist(Current);
        }
    }

    public int GetPageIndex(Workspace workspace) =>
        Math.Clamp(_pageIndexes.GetValueOrDefault(workspace.Id), 0, Math.Max(0, workspace.Pages.Count - 1));

    public void SetPageIndex(Workspace workspace, int index) =>
        _pageIndexes[workspace.Id] = Math.Clamp(index, 0, Math.Max(0, workspace.Pages.Count - 1));

    private void Persist(DeckConfig config)
    {
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(config, JsonOptions));

        // Le token est dans ce fichier : lisible uniquement par l'utilisateur.
        try
        {
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch
        {
            // Non supporté sur cette plateforme : sans gravité.
        }

        File.Move(tmp, _path, overwrite: true);
    }

    private void TryPersist(DeckConfig config)
    {
        try
        {
            Persist(config);
        }
        catch
        {
            // Écriture impossible : on garde l'état en mémoire, sans planter l'interface.
        }
    }
}
