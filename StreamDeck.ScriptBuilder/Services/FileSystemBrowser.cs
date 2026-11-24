namespace StreamDeck.ScriptBuilder.Services;

public record FsEntry(string Name, string Path, bool IsDirectory);

public record FsListing(string Path, string? Parent, IReadOnlyList<FsEntry> Entries, string? Error);

public record FsShortcut(string Label, string Path, string Icon);

/// <summary>Parcourt les disques et dossiers (sélecteur de fichier intégré à l'application).</summary>
public class FileSystemBrowser
{
    /// <summary>Extensions proposées par défaut quand on cherche un programme.</summary>
    public static readonly string[] ProgramExtensions = { ".exe", ".lnk", ".bat", ".cmd", ".msi", ".ps1", ".jar" };

    public IReadOnlyList<FsShortcut> Shortcuts()
    {
        var list = new List<FsShortcut>();

        void Add(string label, string? path, string icon)
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path)) list.Add(new FsShortcut(label, path, icon));
        }

        Add("Bureau", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "fa-solid fa-desktop");
        Add("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "fa-solid fa-folder");
        Add("Programmes", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "fa-solid fa-box");
        Add("Programmes (x86)", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "fa-solid fa-box");
        Add("Menu Démarrer", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows", "Start Menu", "Programs"), "fa-solid fa-bars");
        Add("Menu Démarrer (utilisateur)", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Start Menu", "Programs"), "fa-solid fa-bars");
        Add("Steam", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common"), "fa-brands fa-steam");

        // IsReady peut bloquer longtemps sur un lecteur réseau déconnecté : on ne l'interroge que pour les disques locaux.
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                var local = drive.DriveType is DriveType.Fixed or DriveType.Removable;
                if (local && !drive.IsReady) continue;
                if (!local && drive.DriveType != DriveType.Network) continue;

                list.Add(new FsShortcut($"Disque {drive.Name.TrimEnd('\\', '/')}", drive.RootDirectory.FullName,
                    drive.DriveType == DriveType.Network ? "fa-solid fa-network-wired" : "fa-solid fa-hard-drive"));
            }
        }
        catch (Exception)
        {
            // Lecteurs illisibles : ignorés.
        }

        return list;
    }

    /// <summary>Contenu d'un dossier : les dossiers d'abord, puis les fichiers (filtrés par extensions si demandé).</summary>
    public FsListing List(string? path, IReadOnlyCollection<string>? extensions, bool foldersOnly, bool showHidden = false)
    {
        var folder = string.IsNullOrWhiteSpace(path) ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) : path.Trim().Trim('"');

        // Un chemin de fichier : on affiche son dossier.
        if (File.Exists(folder)) folder = System.IO.Path.GetDirectoryName(folder) ?? folder;

        if (!Directory.Exists(folder))
            return new FsListing(folder, null, Array.Empty<FsEntry>(), "Ce dossier n'existe pas.");

        // Parent : null à la racine d'un disque (GetParent("C:") donnerait un dossier au hasard).
        string? parent = null;
        try { parent = Path.GetDirectoryName(folder.Length > 3 ? folder.TrimEnd('\\', '/') : folder); }
        catch (ArgumentException) { }
        var entries = new List<FsEntry>();

        try
        {
            var info = new DirectoryInfo(folder);

            foreach (var dir in info.EnumerateDirectories().OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                if (!showHidden && dir.Attributes.HasFlag(FileAttributes.Hidden)) continue;
                entries.Add(new FsEntry(dir.Name, dir.FullName, true));
            }

            if (!foldersOnly)
            {
                foreach (var file in info.EnumerateFiles().OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase))
                {
                    if (!showHidden && file.Attributes.HasFlag(FileAttributes.Hidden)) continue;
                    if (extensions is { Count: > 0 } && !extensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase)) continue;
                    entries.Add(new FsEntry(file.Name, file.FullName, false));
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            return new FsListing(folder, parent, entries, "Accès refusé à ce dossier.");
        }
        catch (IOException ex)
        {
            return new FsListing(folder, parent, entries, ex.Message);
        }

        return new FsListing(folder, parent, entries, null);
    }
}

/// <summary>Ce que le sélecteur de fichiers permet de choisir.</summary>
public enum BrowseMode
{
    File,
    Folder,
    FileOrFolder
}
