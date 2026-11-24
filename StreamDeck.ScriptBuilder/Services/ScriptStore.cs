using System.Text;
using System.Text.RegularExpressions;
using StreamDeck.ScriptBuilder.Generation;
using StreamDeck.ScriptBuilder.Models;

namespace StreamDeck.ScriptBuilder.Services;

/// <summary>
/// <paramref name="Id"/> est le nom du script, tel que tapé dans le bouton Script du Stream Deck — sans le dossier :
/// le rangement en dossiers est propre à cette application et n'a aucun effet sur l'appel du script.
/// </summary>
public record ScriptInfo(
    string Id, string Folder, string Path, bool IsBuilderScript, int StepCount, string Description,
    DateTime Modified, IReadOnlyList<StepKind> Kinds, string Keywords, int Order, bool Enabled,
    string Icon, string Color, string Workspace);

/// <param name="Path">Chemin du dossier (« Jeux/Steam »).</param>
public record FolderInfo(string Path, string Name, string Parent, int ScriptCount, int TotalCount, int SubfolderCount, int Order, bool Enabled, string Icon, string Color, string Workspace)
{
    public int Depth => Path.Count(c => c == '/');
}

/// <summary>
/// Une zone de travail contient des dossiers et des scripts de premier niveau (jamais des dossiers ou scripts
/// eux-mêmes : c'est un regroupement, pas un rangement). <see cref="Name"/> vide = la zone « Par défaut », toujours
/// présente, jamais renommable ni supprimable — c'est là que vit tout ce qui n'a pas été rangé ailleurs.
/// </summary>
public record WorkspaceInfo(string Name, string Icon, string Color, int Order, int ItemCount);

/// <summary>État du dossier du serveur NFC.</summary>
public record FolderStatus(
    bool Exists,
    bool HasServer,
    bool HasScriptDir,
    bool HasBuilderUtils,
    bool BuilderUtilsUpToDate,
    bool HasNfcNative,
    bool HasSpotifyHelper)
{
    /// <summary>Assez de choses présentes pour y créer des scripts.</summary>
    public bool Usable => Exists && HasServer;
}

/// <summary>
/// Lit, écrit et supprime les scripts du dossier Script du serveur NFC. Tous les fichiers sont enregistrés directement
/// dans Script (à plat, comme server.ps1 les cherche) : les « dossiers » de la bibliothèque ne sont qu'un rangement
/// propre à cette application, gardé dans les métadonnées du script (et, pour un dossier vide, dans la config).
/// </summary>
public partial class ScriptStore(BuilderConfigService config)
{
    /// <summary>Profondeur maximale des dossiers (Jeux/Steam/Solo = 3).</summary>
    public const int MaxFolderDepth = 3;

    private static readonly UTF8Encoding Utf8Bom = new(encoderShouldEmitUTF8Identifier: true);

    // Mêmes règles que server.ps1 : lettres, chiffres, espace, _ et - ; jamais de « _ » au début (réservé aux actions intégrées).
    [GeneratedRegex(@"^[\p{L}\p{N}][\p{L}\p{N}_ -]*$")]
    private static partial Regex NameRegex();

    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "con", "prn", "aux", "nul", "com1", "com2", "com3", "com4", "lpt1", "lpt2", "lpt3"
    };

    public string ServerFolder => config.Current.ServerFolder;
    public string ScriptsDir => Path.Combine(ServerFolder, "Script");

    public bool HasServerFolder => Inspect(ServerFolder).Usable;

    // ---------- Dossier du serveur ----------

    public static FolderStatus Inspect(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return new FolderStatus(false, false, false, false, false, false, false);

        var utils = Path.Combine(folder, BuilderHelpers.FileName);
        return new FolderStatus(
            Exists: true,
            HasServer: File.Exists(Path.Combine(folder, "server.ps1")),
            HasScriptDir: Directory.Exists(Path.Combine(folder, "Script")),
            HasBuilderUtils: File.Exists(utils),
            BuilderUtilsUpToDate: File.Exists(utils) && ReadUtilsVersion(utils) >= BuilderHelpers.Version,
            HasNfcNative: File.Exists(Path.Combine(folder, "NfcNative.ps1")),
            HasSpotifyHelper: File.Exists(Path.Combine(folder, "SpotifyHelper.ps1")));
    }

    private static int ReadUtilsVersion(string path)
    {
        try
        {
            var match = Regex.Match(File.ReadAllText(path), @"\$SdbUtilsVersion\s*=\s*(\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    /// <summary>Écrit (ou met à jour) BuilderUtils.ps1 et StartServerHidden.vbs à côté de server.ps1.</summary>
    public void EnsureHelpers()
    {
        var status = Inspect(ServerFolder);
        if (!status.Exists) throw new InvalidOperationException("Le dossier du serveur n'existe pas.");

        // StartServerHidden.vbs dépend du programme PowerShell choisi (Réglages) : réécrit chaque fois qu'il pourrait
        // avoir changé, pas seulement à la création — sinon un changement de pwsh.exe -> powershell.exe (ou l'inverse)
        // ne serait jamais répercuté dans un raccourci de démarrage déjà en place.
        File.WriteAllText(Path.Combine(ServerFolder, HiddenLauncher.FileName), HiddenLauncher.Content(config.Current.PowerShellPath), Utf8Bom);

        if (status.BuilderUtilsUpToDate) return;
        File.WriteAllText(Path.Combine(ServerFolder, BuilderHelpers.FileName), BuilderHelpers.Content, Utf8Bom);
    }

    // ---------- Activer / désactiver (le serveur refuse un script désactivé, même appelé directement) ----------

    private string DisabledListPath => Path.Combine(ScriptsDir, ".sdb-disabled.txt");

    private HashSet<string> ReadDisabledNames()
    {
        if (!File.Exists(DisabledListPath)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            return new HashSet<string>(
                File.ReadAllLines(DisabledListPath).Select(l => l.Trim()).Where(l => l.Length > 0),
                StringComparer.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void WriteDisabledNames(HashSet<string> names)
    {
        if (names.Count == 0)
        {
            if (File.Exists(DisabledListPath)) File.Delete(DisabledListPath);
            return;
        }

        Directory.CreateDirectory(ScriptsDir);
        File.WriteAllLines(DisabledListPath, names.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase), Utf8Bom);
    }

    /// <summary>Active ou désactive un script : le serveur refuse de le lancer tant qu'il est désactivé (fichier intact).</summary>
    public void SetScriptEnabled(string id, bool enabled)
    {
        if (!Exists(id)) throw new InvalidOperationException($"« {id} » est introuvable.");

        var names = ReadDisabledNames();
        if (enabled) names.Remove(id); else names.Add(id);
        WriteDisabledNames(names);
    }

    /// <summary>Icône et couleur personnalisées d'un script (l'un ou l'autre vide = valeur par défaut inchangée).</summary>
    public void SetScriptAppearance(string id, string icon, string color)
    {
        if (!Exists(id)) throw new InvalidOperationException($"« {id} » est introuvable.");

        var project = Load(id);
        if (project is not null)
        {
            if (project.Icon == icon && project.Color == color) return;
            project.Icon = icon;
            project.Color = color;
            Save(project, overwrite: true);
            return;
        }

        config.Update(c =>
        {
            if (icon.Length == 0) c.ManualIcon.Remove(id); else c.ManualIcon[id] = icon;
            if (color.Length == 0) c.ManualColor.Remove(id); else c.ManualColor[id] = color;
        });
    }

    /// <summary>Icône et couleur personnalisées d'un dossier (virtuel : tout vit dans la config).</summary>
    public void SetFolderAppearance(string path, string icon, string color)
    {
        var target = NormalizeFolder(path);
        if (!ListFolders().Any(f => f.Path.Equals(target, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"« {target} » est introuvable.");

        config.Update(c =>
        {
            if (icon.Length == 0) c.FolderIcon.Remove(target); else c.FolderIcon[target] = icon;
            if (color.Length == 0) c.FolderColor.Remove(target); else c.FolderColor[target] = color;
        });
    }

    /// <summary>Range automatiquement (ordre alphabétique) les dossiers et scripts d'un dossier — les dossiers d'abord, puis les scripts.</summary>
    public void SortFolder(string folder, bool recursive = false)
    {
        var target = NormalizeFolder(folder);

        var folders = ListFolders().Where(f => f.Parent.Equals(target, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(f => (IsFolder: true, Id: f.Path))
            .ToList();
        var scripts = List().Where(s => s.Folder.Equals(target, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Id, StringComparer.CurrentCultureIgnoreCase)
            .Select(s => (IsFolder: false, Id: s.Id));

        var ordered = folders.Concat(scripts).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].IsFolder) SetFolderOrder(ordered[i].Id, i);
            else SetOrder(ordered[i].Id, i);
        }

        if (recursive)
            foreach (var sub in folders) SortFolder(sub.Id, recursive: true);
    }

    // ---------- Zones de travail : contiennent des dossiers et scripts de premier niveau, ce ne sont pas des dossiers ----------

    /// <summary>Zone d'appartenance d'un élément de premier niveau (nom vide = « Par défaut »).</summary>
    private string WorkspaceOfRoot(string rootId) =>
        config.Current.RootWorkspace.TryGetValue(rootId, out var w) ? w : "";

    /// <summary>Toutes les zones, « Par défaut » toujours en premier (jamais dans la config : implicite).</summary>
    public IReadOnlyList<WorkspaceInfo> ListWorkspaces()
    {
        var scripts = List();
        var folders = ListFolders();

        int CountOf(string name) =>
            scripts.Count(s => s.Workspace.Equals(name, StringComparison.OrdinalIgnoreCase)) +
            folders.Count(f => f.Depth == 0 && f.Workspace.Equals(name, StringComparison.OrdinalIgnoreCase));

        var named = config.Current.Workspaces
            .OrderBy(w => w.Order).ThenBy(w => w.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(w => new WorkspaceInfo(w.Name, w.Icon, w.Color, w.Order, CountOf(w.Name)));

        return new[] { new WorkspaceInfo("", "", "", -1, CountOf("")) }.Concat(named).ToList();
    }

    /// <summary>Crée une nouvelle zone de travail, vide au départ.</summary>
    public void CreateWorkspace(string name, string icon = "", string color = "")
    {
        name = name.Trim();
        if (name.Length == 0) throw new InvalidOperationException("Le nom de la zone de travail est vide.");
        if (config.Current.Workspaces.Any(w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Une zone de travail « {name} » existe déjà.");

        var nextOrder = config.Current.Workspaces.Count == 0 ? 0 : config.Current.Workspaces.Max(w => w.Order) + 1;
        config.Update(c => c.Workspaces.Add(new StoredWorkspace { Name = name, Icon = icon, Color = color, Order = nextOrder }));
    }

    public void RenameWorkspace(string oldName, string newName)
    {
        newName = newName.Trim();
        if (oldName.Length == 0) throw new InvalidOperationException("La zone « Par défaut » ne peut pas être renommée.");
        if (newName.Length == 0) throw new InvalidOperationException("Le nouveau nom est vide.");
        var existing = config.Current.Workspaces.FirstOrDefault(w => w.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"« {oldName} » est introuvable.");
        if (!oldName.Equals(newName, StringComparison.OrdinalIgnoreCase) && config.Current.Workspaces.Any(w => w.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Une zone de travail « {newName} » existe déjà.");

        config.Update(c =>
        {
            var w = c.Workspaces.First(w => w.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase));
            w.Name = newName;
            foreach (var key in c.RootWorkspace.Keys.Where(k => c.RootWorkspace[k].Equals(oldName, StringComparison.OrdinalIgnoreCase)).ToList())
                c.RootWorkspace[key] = newName;
        });
    }

    public void SetWorkspaceAppearance(string name, string icon, string color)
    {
        if (name.Length == 0) throw new InvalidOperationException("La zone « Par défaut » ne peut pas être personnalisée.");
        config.Update(c =>
        {
            var w = c.Workspaces.FirstOrDefault(w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"« {name} » est introuvable.");
            w.Icon = icon;
            w.Color = color;
        });
    }

    /// <summary>Supprime la zone : son contenu revient à « Par défaut » (jamais supprimé).</summary>
    public void DeleteWorkspace(string name)
    {
        if (name.Length == 0) throw new InvalidOperationException("La zone « Par défaut » ne peut pas être supprimée.");
        config.Update(c =>
        {
            c.Workspaces.RemoveAll(w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            foreach (var key in c.RootWorkspace.Keys.Where(k => c.RootWorkspace[k].Equals(name, StringComparison.OrdinalIgnoreCase)).ToList())
                c.RootWorkspace.Remove(key);
        });
    }

    /// <summary>
    /// Range un dossier ou un script DE PREMIER NIVEAU dans une zone de travail (nom vide = « Par défaut »). Pour un
    /// élément niché, déplace-le d'abord à la racine (<see cref="SetFolder"/> / <see cref="MoveFolder"/>) : la zone
    /// se règle uniquement sur l'ancêtre de premier niveau, jamais sur un élément imbriqué.
    /// </summary>
    public void SetItemWorkspace(bool isFolder, string id, string workspace)
    {
        workspace = workspace.Trim();
        if (workspace.Length > 0 && !config.Current.Workspaces.Any(w => w.Name.Equals(workspace, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"La zone de travail « {workspace} » est introuvable.");

        if (isFolder)
        {
            var folder = ListFolders().FirstOrDefault(f => f.Path.Equals(id, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"« {id} » est introuvable.");
            if (folder.Depth != 0) throw new InvalidOperationException("Seul un dossier de premier niveau peut être rangé dans une zone de travail.");
        }
        else
        {
            var script = List().FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"« {id} » est introuvable.");
            if (script.Folder.Length != 0) throw new InvalidOperationException("Seul un script de premier niveau (hors dossier) peut être rangé dans une zone de travail.");
        }

        config.Update(c =>
        {
            if (workspace.Length == 0) c.RootWorkspace.Remove(id);
            else c.RootWorkspace[id] = workspace;
        });
    }

    public bool IsFolderDisabled(string folder) => config.Current.DisabledFolders.Contains(NormalizeFolder(folder));

    /// <summary>Active ou désactive tous les scripts d'un dossier (et de ses sous-dossiers) d'un coup, côté serveur.</summary>
    public void SetFolderEnabled(string folder, bool enabled)
    {
        var target = NormalizeFolder(folder);
        var names = ReadDisabledNames();

        foreach (var script in List().Where(s => s.Folder.Equals(target, StringComparison.OrdinalIgnoreCase) || s.Folder.StartsWith(target + "/", StringComparison.OrdinalIgnoreCase)))
        {
            if (enabled) names.Remove(script.Id); else names.Add(script.Id);
        }
        WriteDisabledNames(names);

        config.Update(c =>
        {
            if (enabled) c.DisabledFolders.Remove(target);
            else c.DisabledFolders.Add(target);
        });
    }

    // ---------- Noms et dossiers ----------

    /// <summary>Message d'erreur si le nom ne convient pas comme nom de script ou de dossier, sinon null.</summary>
    public static string? NameError(string? name)
    {
        var n = (name ?? string.Empty).Trim();
        if (n.Length == 0) return "Donne un nom.";
        if (n.Length > 60) return "Nom trop long (60 caractères au maximum).";
        if (n.StartsWith('_')) return "Le nom ne peut pas commencer par « _ » (réservé au serveur).";
        if (!NameRegex().IsMatch(n)) return "Lettres, chiffres, espaces, « _ » et « - » uniquement (le nom sert d'adresse pour Stream Deck).";
        if (Reserved.Contains(n)) return "Ce nom est réservé par Windows.";
        return null;
    }

    /// <summary>« Jeux\Steam/ » devient « Jeux/Steam » (vide = aucun dossier).</summary>
    public static string NormalizeFolder(string? folder) =>
        string.Join('/', (folder ?? string.Empty).Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    /// <summary>Message d'erreur si le dossier est invalide (null si valide ou vide = aucun dossier).</summary>
    public static string? FolderError(string? folder)
    {
        var f = NormalizeFolder(folder);
        if (f.Length == 0) return null;

        var parts = f.Split('/');
        if (parts.Length > MaxFolderDepth) return $"Trop de niveaux de dossiers ({MaxFolderDepth} au maximum).";

        foreach (var part in parts)
        {
            var error = NameError(part);
            if (error is not null) return $"Dossier « {part} » : {error}";
        }

        return null;
    }

    public string PathFor(string name)
    {
        var error = NameError(name);
        if (error is not null) throw new ArgumentException(error);
        return Path.Combine(ScriptsDir, name.Trim() + ".ps1");
    }

    /// <summary>
    /// Emplacement réel d'un script existant : à plat dans Script en premier, sinon (script hérité d'une version de
    /// l'application qui rangeait dans de vrais sous-dossiers, ou déposé à la main) dans ses sous-dossiers. Null si
    /// aucun fichier de ce nom n'existe nulle part.
    /// </summary>
    private string? ResolveExistingPath(string name)
    {
        var flat = PathFor(name);
        if (File.Exists(flat)) return flat;
        if (!Directory.Exists(ScriptsDir)) return null;

        return Directory.EnumerateFiles(ScriptsDir, name.Trim() + ".ps1", SearchOption.AllDirectories).FirstOrDefault();
    }

    public bool Exists(string name)
    {
        try { return ResolveExistingPath(name) is not null; }
        catch (ArgumentException) { return false; }
    }

    // ---------- Lecture ----------

    /// <summary>
    /// Tous les scripts. Toujours enregistrés à plat dans Script (server.ps1 les cherche ainsi) ; ceux trouvés dans un
    /// sous-dossier réel (rangés par une version précédente de l'application) restent lisibles, groupés par ce sous-dossier.
    /// </summary>
    public IReadOnlyList<ScriptInfo> List()
    {
        if (!Directory.Exists(ScriptsDir)) return Array.Empty<ScriptInfo>();

        var root = Path.GetFullPath(ScriptsDir);
        var result = new List<ScriptInfo>();
        var disabled = ReadDisabledNames();

        foreach (var file in Directory.EnumerateFiles(root, "*.ps1", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file)[..^4].Replace('\\', '/');
            var isNested = relative.Contains('/');
            var name = isNested ? relative[(relative.LastIndexOf('/') + 1)..] : relative;
            if (NameError(name) is not null) continue;

            try
            {
                var project = ScriptGenerator.TryReadProject(File.ReadAllText(file));
                // Dossier : pour un script du constructeur, ses métadonnées ; pour un script écrit à la main, son
                // rangement dans la config s'il en a un, sinon l'emplacement réel du fichier (script hérité d'une
                // version antérieure, ou déposé à la main dans un sous-dossier).
                var folder = project is not null
                    ? NormalizeFolder(project.Folder)
                    : config.Current.ManualFolders.TryGetValue(name, out var manualFolder)
                        ? NormalizeFolder(manualFolder)
                        : (isNested ? relative[..relative.LastIndexOf('/')] : string.Empty);
                var kinds = project?.Steps.Select(s => s.Kind).ToList() ?? new List<StepKind>();
                var keywords = project is null ? string.Empty : string.Join(' ', project.Steps.Select(ScriptGenerator.Title));
                var order = project?.Order ?? (config.Current.ManualOrder.TryGetValue(name, out var manualOrder) ? manualOrder : 0);
                var icon = project is not null ? project.Icon : config.Current.ManualIcon.TryGetValue(name, out var mIcon) ? mIcon : "";
                var color = project is not null ? project.Color : config.Current.ManualColor.TryGetValue(name, out var mColor) ? mColor : "";
                var workspace = WorkspaceOfRoot(folder.Length > 0 ? folder.Split('/')[0] : name);
                result.Add(new ScriptInfo(name, folder, file, project is not null, project?.Steps.Count ?? 0,
                    project?.Description ?? string.Empty, File.GetLastWriteTime(file), kinds, keywords, order, !disabled.Contains(name),
                    icon, color, workspace));
            }
            catch (IOException)
            {
                // Fichier verrouillé : ignoré dans la liste.
            }
        }

        return result.OrderBy(s => s.Folder, StringComparer.CurrentCultureIgnoreCase)
                     .ThenBy(s => s.Order)
                     .ThenBy(s => s.Id, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>Tous les dossiers (même vides) : ceux créés explicitement, plus ceux utilisés par au moins un script.</summary>
    public IReadOnlyList<FolderInfo> ListFolders(IReadOnlyList<ScriptInfo>? scripts = null)
    {
        scripts ??= List();

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void AddWithParents(string path)
        {
            var p = path;
            while (p.Length > 0)
            {
                paths.Add(p);
                p = SplitId(p).Folder;
            }
        }

        foreach (var c in config.Current.Collections) if (FolderError(c) is null) AddWithParents(NormalizeFolder(c));
        foreach (var s in scripts) if (s.Folder.Length > 0) AddWithParents(s.Folder);

        return paths
            .OrderBy(p => SplitId(p).Folder, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => config.Current.FolderOrder.TryGetValue(p, out var o) ? o : 0)
            .ThenBy(p => p, StringComparer.CurrentCultureIgnoreCase)
            .Select(p =>
            {
                var (parent, name) = SplitId(p);
                return new FolderInfo(p, name, parent,
                    scripts.Count(s => s.Folder.Equals(p, StringComparison.OrdinalIgnoreCase)),
                    scripts.Count(s => s.Folder.Equals(p, StringComparison.OrdinalIgnoreCase) || s.Folder.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase)),
                    paths.Count(o => SplitId(o).Folder.Equals(p, StringComparison.OrdinalIgnoreCase)),
                    config.Current.FolderOrder.TryGetValue(p, out var order) ? order : 0,
                    !config.Current.DisabledFolders.Contains(p),
                    config.Current.FolderIcon.TryGetValue(p, out var icon) ? icon : "",
                    config.Current.FolderColor.TryGetValue(p, out var color) ? color : "",
                    WorkspaceOfRoot(p.Split('/')[0]));
            })
            .ToList();
    }

    private static (string Folder, string Name) SplitId(string path)
    {
        var i = path.LastIndexOf('/');
        return i < 0 ? (string.Empty, path) : (path[..i], path[(i + 1)..]);
    }

    /// <summary>Texte d'un script existant, où qu'il se trouve réellement sur le disque.</summary>
    public string ReadText(string id)
    {
        var path = ResolveExistingPath(id) ?? throw new FileNotFoundException($"« {id} » est introuvable dans {ScriptsDir}.", PathFor(id));
        return File.ReadAllText(path);
    }

    /// <summary>Projet d'un script créé par l'application (null pour un script écrit à la main).</summary>
    public ScriptProject? Load(string id)
    {
        var project = ScriptGenerator.TryReadProject(ReadText(id));
        if (project is null) return null;

        project.Name = id.Trim();
        project.Folder = NormalizeFolder(project.Folder);
        return project;
    }

    // ---------- Écriture ----------

    /// <summary>Enregistre le script, toujours directement dans Script. Renvoie le chemin du fichier.</summary>
    public string Save(ScriptProject project, bool overwrite)
    {
        var error = NameError(project.Name) ?? FolderError(project.Folder);
        if (error is not null) throw new InvalidOperationException(error);
        if (!HasServerFolder) throw new InvalidOperationException("Le dossier du serveur n'est pas configuré (Réglages).");

        project.Name = project.Name.Trim();
        project.Folder = NormalizeFolder(project.Folder);

        var path = PathFor(project.Name);
        var existing = ResolveExistingPath(project.Name);
        if (existing is not null && !overwrite) throw new IOException("Un script porte déjà ce nom.");

        // Nouveau script : il prend place à la fin de son dossier plutôt que de se mêler aux scripts déjà rangés.
        if (existing is null && project.Order == 0)
        {
            var siblingOrders = List().Where(s => s.Folder.Equals(project.Folder, StringComparison.OrdinalIgnoreCase)).Select(s => s.Order).ToList();
            if (siblingOrders.Count > 0) project.Order = siblingOrders.Max() + 1;
        }

        Directory.CreateDirectory(ScriptsDir);
        EnsureHelpers();

        var tmp = path + ".tmp";
        File.WriteAllText(tmp, ScriptGenerator.Generate(project, config.Current.PhoneTargets, config.Current.DefaultPhoneId), Utf8Bom);
        File.Move(tmp, path, overwrite: true);

        // Migration : si le script vivait dans un sous-dossier réel (version antérieure de l'application), l'ancien
        // fichier est supprimé maintenant qu'une copie à plat existe.
        if (existing is not null && !existing.Equals(path, StringComparison.OrdinalIgnoreCase)) File.Delete(existing);

        // Nouveau script dans un dossier désactivé : il hérite du réglage (protection par défaut, plutôt que de
        // laisser un script filer entre les mailles d'un dossier coupé).
        if (existing is null && IsFolderDisabled(project.Folder)) SetScriptEnabled(project.Name, false);

        return path;
    }

    public void Delete(string id)
    {
        var path = ResolveExistingPath(id);
        if (path is not null) File.Delete(path);

        // Nettoie une éventuelle étiquette « désactivé » : un futur script portant le même nom ne doit pas en hériter.
        var disabled = ReadDisabledNames();
        if (disabled.Remove(id)) WriteDisabledNames(disabled);

        if (config.Current.ManualIcon.ContainsKey(id) || config.Current.ManualColor.ContainsKey(id) || config.Current.ManualOrder.ContainsKey(id))
            config.Update(c => { c.ManualIcon.Remove(id); c.ManualColor.Remove(id); c.ManualOrder.Remove(id); });
        if (config.Current.RootWorkspace.ContainsKey(id))
            config.Update(c => c.RootWorkspace.Remove(id));
    }

    /// <summary>Copie un script (même dossier) sous un nouveau nom. Renvoie le nouveau nom.</summary>
    public string Duplicate(string id)
    {
        var baseName = id + " copie";
        var candidate = baseName;
        for (var i = 2; Exists(candidate); i++) candidate = $"{baseName} {i}";

        var project = Load(id);
        if (project is null)
        {
            File.Copy(ResolveExistingPath(id) ?? throw new FileNotFoundException($"« {id} » est introuvable dans {ScriptsDir}."), PathFor(candidate));
        }
        else
        {
            project.Name = candidate;
            Save(project, overwrite: false);
        }

        return candidate;
    }

    /// <summary>
    /// Range un script dans un autre dossier (« » = aucun dossier). Le fichier ne bouge jamais sur le disque (le nom
    /// d'appel ne change donc pas) : pour un script du constructeur, seules ses métadonnées changent ; pour un script
    /// écrit à la main (dont le fichier n'est jamais modifié par l'application), le rangement est gardé dans la config.
    /// </summary>
    public void SetFolder(string id, string targetFolder)
    {
        var target = NormalizeFolder(targetFolder);
        var error = FolderError(target);
        if (error is not null) throw new InvalidOperationException(error);
        if (!Exists(id)) throw new InvalidOperationException($"« {id} » est introuvable.");

        var project = Load(id);
        if (project is not null)
        {
            if (config.Current.ManualFolders.ContainsKey(id)) config.Update(c => c.ManualFolders.Remove(id));
            if (!project.Folder.Equals(target, StringComparison.OrdinalIgnoreCase))
            {
                project.Folder = target;
                Save(project, overwrite: true);
            }
        }
        else
        {
            config.Update(c =>
            {
                if (target.Length == 0) c.ManualFolders.Remove(id);
                else c.ManualFolders[id] = target;
            });
        }

        // Déplacé dans un dossier désactivé : suit la même protection par défaut qu'un nouveau script.
        if (IsFolderDisabled(target)) SetScriptEnabled(id, false);
    }

    /// <summary>Position (glisser-déposer) d'un script, quel que soit son type — écrit dans les métadonnées pour un
    /// script du constructeur, ou dans la config pour un script écrit à la main (dont le fichier n'est jamais touché).</summary>
    private void SetOrder(string id, int order)
    {
        var project = Load(id);
        if (project is not null)
        {
            if (project.Order == order) return;
            project.Order = order;
            Save(project, overwrite: true);
            return;
        }

        config.Update(c => c.ManualOrder[id] = order);
    }

    /// <summary>
    /// Glisser-déposer : positionne <paramref name="id"/> juste avant <paramref name="beforeId"/> — dans le même
    /// dossier que la cible (change son rangement si besoin), et renumérote les autres scripts du dossier en
    /// conséquence pour que l'ordre choisi reste stable.
    /// </summary>
    /// <summary>Repositionne un script juste avant un autre (même dossier que la cible, quitte à l'y ranger).</summary>
    /// <summary>Repositionne un script juste avant un autre (compatibilité ; préférer <see cref="ReorderItemToIndex"/>).</summary>
    public void ReorderBefore(string id, string beforeId) => ReorderItemBefore(false, id, false, beforeId);

    /// <summary>
    /// Glisser-déposer unifié : positionne l'élément (dossier ou script) identifié par <paramref name="isFolder"/> +
    /// <paramref name="id"/> juste avant la cible — dans le même dossier parent que la cible, quitte à l'y ranger.
    /// Compatibilité (préférer <see cref="ReorderItemToIndex"/>, qui exprime aussi « après le dernier »).
    /// </summary>
    public void ReorderItemBefore(bool isFolder, string id, bool targetIsFolder, string targetId)
    {
        if (isFolder == targetIsFolder && id.Equals(targetId, StringComparison.OrdinalIgnoreCase)) return;

        var targetParent = targetIsFolder ? SplitId(targetId).Folder : ScriptFolderOf(targetId);
        var index = OrderedItems(targetParent)
            .Where(x => !(x.IsFolder == isFolder && x.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
            .TakeWhile(x => !(x.IsFolder == targetIsFolder && x.Id.Equals(targetId, StringComparison.OrdinalIgnoreCase)))
            .Count();

        ReorderItemToIndex(isFolder, id, targetParent, index);
    }

    /// <summary>
    /// Glisser-déposer : positionne l'élément (dossier ou script) à l'INDEX précis <paramref name="index"/> parmi les
    /// dossiers et scripts mélangés de <paramref name="parentFolder"/> (0 = tout en premier, nombre d'éléments = tout
    /// en dernier). Sans ambiguïté « avant / après » : l'index dit exactement où l'élément atterrit.
    /// </summary>
    public void ReorderItemToIndex(bool isFolder, string id, string parentFolder, int index)
    {
        var target = NormalizeFolder(parentFolder);
        if (isFolder && (target.Equals(id, StringComparison.OrdinalIgnoreCase) || target.StartsWith(id + "/", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Un dossier ne peut pas être positionné à l'intérieur de lui-même.");

        var currentParent = isFolder ? SplitId(id).Folder : ScriptFolderOf(id);
        if (!currentParent.Equals(target, StringComparison.OrdinalIgnoreCase))
        {
            if (isFolder) MoveFolder(id, target);
            else SetFolder(id, target);
        }

        var ordered = OrderedItems(target).ToList();
        ordered.RemoveAll(x => x.IsFolder == isFolder && x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        var clamped = Math.Clamp(index, 0, ordered.Count);
        ordered.Insert(clamped, (isFolder, id));

        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].IsFolder) SetFolderOrder(ordered[i].Id, i);
            else SetOrder(ordered[i].Id, i);
        }
    }

    /// <summary>Dossiers et scripts d'un même dossier parent, mélangés dans leur ordre actuel.</summary>
    private IEnumerable<(bool IsFolder, string Id)> OrderedItems(string parentFolder)
    {
        var siblingFolders = ListFolders()
            .Where(f => f.Parent.Equals(parentFolder, StringComparison.OrdinalIgnoreCase))
            .Select(f => (IsFolder: true, Id: f.Path, f.Order, Key: f.Name));
        var siblingScripts = List()
            .Where(s => s.Folder.Equals(parentFolder, StringComparison.OrdinalIgnoreCase))
            .Select(s => (IsFolder: false, Id: s.Id, s.Order, Key: s.Id));

        // Départage par le nom AFFICHÉ (dernier segment pour un dossier), jamais par le chemin complet : comparer
        // « Jeux/Steam » à « Soirée Steam » n'aurait aucun sens (un dossier serait toujours mal classé selon son parent).
        return siblingFolders.Concat(siblingScripts)
            .OrderBy(x => x.Order).ThenBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => (x.IsFolder, x.Id));
    }

    private string ScriptFolderOf(string id) =>
        List().FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.Folder
        ?? throw new InvalidOperationException($"« {id} » est introuvable.");

    private void SetFolderOrder(string path, int order)
    {
        if (config.Current.FolderOrder.TryGetValue(path, out var current) && current == order) return;
        config.Update(c => c.FolderOrder[path] = order);
    }

    // ---------- Dossiers ----------

    public void CreateFolder(string folder)
    {
        var target = NormalizeFolder(folder);
        var error = FolderError(target);
        if (error is not null) throw new InvalidOperationException(error);
        if (target.Length == 0) throw new InvalidOperationException("Donne un nom au dossier.");
        if (ListFolders().Any(f => f.Path.Equals(target, StringComparison.OrdinalIgnoreCase))) throw new IOException("Ce dossier existe déjà.");

        config.Update(c =>
        {
            if (!c.Collections.Contains(target, StringComparer.OrdinalIgnoreCase)) c.Collections.Add(target);
        });
    }

    /// <summary>Renomme le dernier niveau d'un dossier (les scripts et sous-dossiers suivent). Renvoie son nouveau chemin.</summary>
    public string RenameFolder(string folder, string newName)
    {
        var error = NameError(newName);
        if (error is not null) throw new InvalidOperationException(error);

        var source = NormalizeFolder(folder);
        var (parent, _) = SplitId(source);
        var target = parent.Length == 0 ? newName.Trim() : parent + "/" + newName.Trim();
        if (string.Equals(source, target, StringComparison.Ordinal)) return target;

        if (ListFolders().Any(f => f.Path.Equals(target, StringComparison.OrdinalIgnoreCase)))
            throw new IOException("Un dossier porte déjà ce nom.");

        Reparent(source, target);
        return target;
    }

    /// <summary>Déplace un dossier (et son contenu) sous un autre parent (« » = à la racine).</summary>
    public string MoveFolder(string folder, string newParent)
    {
        var source = NormalizeFolder(folder);
        var parent = NormalizeFolder(newParent);
        var (_, name) = SplitId(source);
        var target = parent.Length == 0 ? name : parent + "/" + name;

        if (string.Equals(source, target, StringComparison.Ordinal)) return target;
        if (target == source || (target + "/").StartsWith(source + "/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Un dossier ne peut pas être déplacé dans lui-même.");
        if (FolderError(target) is { } error) throw new InvalidOperationException(error);
        if (ListFolders().Any(f => f.Path.Equals(target, StringComparison.OrdinalIgnoreCase)))
            throw new IOException("Un dossier porte déjà ce nom à cet endroit.");

        Reparent(source, target);
        return target;
    }

    /// <summary>Renomme/déplace un dossier virtuel : met à jour la config et les scripts (et sous-dossiers) concernés.</summary>
    private void Reparent(string source, string target)
    {
        foreach (var script in List().Where(s => s.Folder.Equals(source, StringComparison.OrdinalIgnoreCase) || s.Folder.StartsWith(source + "/", StringComparison.OrdinalIgnoreCase)))
        {
            var newFolder = target + script.Folder[source.Length..];

            if (Load(script.Id) is { } project)
            {
                project.Folder = newFolder;
                Save(project, overwrite: true);
            }
            else
            {
                // Script écrit à la main : seul son étiquetage dans la config change, jamais le fichier.
                config.Update(c => c.ManualFolders[script.Id] = newFolder);
            }
        }

        config.Update(c =>
        {
            for (var i = 0; i < c.Collections.Count; i++)
            {
                if (c.Collections[i].Equals(source, StringComparison.OrdinalIgnoreCase)) c.Collections[i] = target;
                else if (c.Collections[i].StartsWith(source + "/", StringComparison.OrdinalIgnoreCase)) c.Collections[i] = target + c.Collections[i][source.Length..];
            }

            // Étiquettes (position, dossier désactivé) associées à l'ancien chemin : reportées sur le nouveau.
            foreach (var oldPath in c.FolderOrder.Keys.Where(p => p.Equals(source, StringComparison.OrdinalIgnoreCase) || p.StartsWith(source + "/", StringComparison.OrdinalIgnoreCase)).ToList())
            {
                var newPath = oldPath.Equals(source, StringComparison.OrdinalIgnoreCase) ? target : target + oldPath[source.Length..];
                c.FolderOrder[newPath] = c.FolderOrder[oldPath];
                c.FolderOrder.Remove(oldPath);
            }

            foreach (var oldPath in c.DisabledFolders.Where(p => p.Equals(source, StringComparison.OrdinalIgnoreCase) || p.StartsWith(source + "/", StringComparison.OrdinalIgnoreCase)).ToList())
            {
                var newPath = oldPath.Equals(source, StringComparison.OrdinalIgnoreCase) ? target : target + oldPath[source.Length..];
                c.DisabledFolders.Remove(oldPath);
                c.DisabledFolders.Add(newPath);
            }

            // Zone de travail : n'existe que pour un élément de PREMIER NIVEAU. Reste top-level après coup -> migrée
            // vers le nouveau nom. Devient (ou était déjà) niché -> l'étiquette n'a plus de sens, retirée.
            if (c.RootWorkspace.Remove(source, out var ws))
            {
                if (!target.Contains('/')) c.RootWorkspace[target] = ws;
            }

            MigrateKeys(c.FolderIcon, source, target);
            MigrateKeys(c.FolderColor, source, target);

            static void MigrateKeys(Dictionary<string, string> dict, string source, string target)
            {
                foreach (var oldPath in dict.Keys.Where(p => p.Equals(source, StringComparison.OrdinalIgnoreCase) || p.StartsWith(source + "/", StringComparison.OrdinalIgnoreCase)).ToList())
                {
                    var newPath = oldPath.Equals(source, StringComparison.OrdinalIgnoreCase) ? target : target + oldPath[source.Length..];
                    dict[newPath] = dict[oldPath];
                    dict.Remove(oldPath);
                }
            }
        });
    }

    /// <summary>Supprime un dossier. Refuse un dossier non vide sauf si <paramref name="includeContents"/>.</summary>
    public void DeleteFolder(string folder, bool includeContents)
    {
        var target = NormalizeFolder(folder);
        if (target.Length == 0) throw new InvalidOperationException("Ce dossier ne peut pas être supprimé.");

        var scripts = List();
        var inside = scripts.Where(s => s.Folder.Equals(target, StringComparison.OrdinalIgnoreCase) || s.Folder.StartsWith(target + "/", StringComparison.OrdinalIgnoreCase)).ToList();
        var hasSubfolders = ListFolders(scripts).Any(f => f.Parent.Equals(target, StringComparison.OrdinalIgnoreCase) || f.Path.StartsWith(target + "/", StringComparison.OrdinalIgnoreCase));

        if (!includeContents && (inside.Count > 0 || hasSubfolders)) throw new IOException("Le dossier n'est pas vide.");

        if (includeContents)
            foreach (var script in inside)
            {
                // Un script du constructeur est supprimé avec son dossier ; un script écrit à la main n'est jamais
                // supprimé par l'application (ce n'est pas elle qui l'a créé) : il est seulement dérangé (retour à la racine).
                if (script.IsBuilderScript) Delete(script.Id);
                else if (config.Current.ManualFolders.ContainsKey(script.Id)) config.Update(c => c.ManualFolders.Remove(script.Id));
            }

        config.Update(c =>
        {
            c.Collections.RemoveAll(p => p.Equals(target, StringComparison.OrdinalIgnoreCase) || p.StartsWith(target + "/", StringComparison.OrdinalIgnoreCase));
            c.FolderOrder.Keys.Where(p => p.Equals(target, StringComparison.OrdinalIgnoreCase) || p.StartsWith(target + "/", StringComparison.OrdinalIgnoreCase)).ToList()
                .ForEach(p => c.FolderOrder.Remove(p));
            c.DisabledFolders.RemoveWhere(p => p.Equals(target, StringComparison.OrdinalIgnoreCase) || p.StartsWith(target + "/", StringComparison.OrdinalIgnoreCase));
            c.FolderIcon.Keys.Where(p => p.Equals(target, StringComparison.OrdinalIgnoreCase) || p.StartsWith(target + "/", StringComparison.OrdinalIgnoreCase)).ToList().ForEach(p => c.FolderIcon.Remove(p));
            c.FolderColor.Keys.Where(p => p.Equals(target, StringComparison.OrdinalIgnoreCase) || p.StartsWith(target + "/", StringComparison.OrdinalIgnoreCase)).ToList().ForEach(p => c.FolderColor.Remove(p));
            c.RootWorkspace.Remove(target);
        });
    }
}
