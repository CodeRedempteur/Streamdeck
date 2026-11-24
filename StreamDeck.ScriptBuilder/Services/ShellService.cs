using System.Diagnostics;

namespace StreamDeck.ScriptBuilder.Services;

/// <summary>Ouvre un fichier dans l'éditeur par défaut ou l'Explorateur Windows.</summary>
public class ShellService
{
    public bool OpenFile(string path) => Start(new ProcessStartInfo(path) { UseShellExecute = true });

    /// <summary>Ouvre l'Explorateur en sélectionnant le fichier.</summary>
    public bool RevealInExplorer(string path)
    {
        var info = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
        info.ArgumentList.Add("/select,");
        info.ArgumentList.Add(path);
        return Start(info);
    }

    private static bool Start(ProcessStartInfo info)
    {
        try
        {
            using var process = Process.Start(info);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
