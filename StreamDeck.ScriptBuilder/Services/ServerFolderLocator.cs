namespace StreamDeck.ScriptBuilder.Services;

/// <summary>Cherche le dossier du serveur NFC (celui qui contient server.ps1 et un dossier Script) aux endroits habituels.</summary>
public static class ServerFolderLocator
{
    private const int MaxDepth = 3;

    public static IReadOnlyList<string> FindCandidates(IEnumerable<string>? extraRoots = null)
    {
        var roots = new List<string>();
        foreach (var folder in new[]
                 {
                     Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolder.MyDocuments,
                     Environment.SpecialFolder.UserProfile, Environment.SpecialFolder.LocalApplicationData
                 })
        {
            var path = Environment.GetFolderPath(folder);
            if (!string.IsNullOrEmpty(path)) roots.Add(path);
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(profile))
            foreach (var sub in new[] { "Downloads", "source", "Sources", "OneDrive", "OneDrive\\Desktop", "OneDrive\\Documents" })
                roots.Add(Path.Combine(profile, sub));

        if (extraRoots is not null) roots.AddRange(extraRoots);

        // Racines de disques : un niveau seulement (C:\NFC, C:\Scripts…), pas tout le disque.
        try
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
                roots.Add(drive.RootDirectory.FullName);
        }
        catch (Exception)
        {
            // Lecteurs illisibles : ignorés.
        }

        var found = new List<string>();
        foreach (var root in roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
            Scan(root, 0, found);

        return found.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void Scan(string folder, int depth, List<string> found)
    {
        try
        {
            if (File.Exists(Path.Combine(folder, "server.ps1")) && Directory.Exists(Path.Combine(folder, "Script")))
            {
                found.Add(folder);
                return;
            }

            if (depth >= MaxDepth) return;

            foreach (var sub in Directory.EnumerateDirectories(folder))
            {
                var name = Path.GetFileName(sub);
                if (name.StartsWith('.') || name is "node_modules" or "bin" or "obj" or "Windows" or "Program Files"
                    or "Program Files (x86)" or "ProgramData" or "AppData" or "$Recycle.Bin")
                    continue;

                Scan(sub, depth + 1, found);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // Dossier inaccessible : on passe au suivant.
        }
    }
}
