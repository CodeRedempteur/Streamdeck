using System.Diagnostics;
using StreamDeck.ScriptBuilder.Models;

namespace StreamDeck.ScriptBuilder.Services;

public record RunningWindow(string ProcessName, string Title);

/// <summary>Programmes actuellement ouverts (pour choisir un nom de processus sans le taper).</summary>
public class ProcessService
{
    public IReadOnlyList<RunningWindow> RunningWindows()
    {
        var list = new List<RunningWindow>();

        try
        {
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(p.MainWindowTitle))
                        list.Add(new RunningWindow(p.ProcessName, p.MainWindowTitle));
                }
                catch (Exception)
                {
                    // Processus protégé ou terminé pendant la lecture : ignoré.
                }
                finally
                {
                    p.Dispose();
                }
            }
        }
        catch (Exception)
        {
            // Liste des processus inaccessible : on renvoie ce qu'on a.
        }

        return list
            .GroupBy(w => w.ProcessName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(w => w.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
