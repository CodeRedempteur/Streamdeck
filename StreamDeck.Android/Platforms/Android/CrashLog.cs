using Android.Runtime;
using Microsoft.Maui.Storage;
using StreamDeck.Services;

namespace StreamDeck.Mobile;

/// <summary>
/// v1.34.20 : garde la trace du dernier plantage non rattrapé dans un fichier texte (dossier interne de l'app,
/// <see cref="Android.Content.Context.FilesDir"/>, pas besoin de permission de stockage), pour pouvoir l'afficher
/// dans les Réglages (voir <see cref="AndroidCrashLogProvider"/>/DiagnosticsSection.razor côté StreamDeck.Shared) —
/// sans ça, un plantage sur un appareil sans PC/adb à portée de main (câble, pilotes...) n'est pas diagnosticable à
/// distance. Écoute trois sources (AndroidEnvironment, qui couvre la plupart des plantages JNI/Mono sur Android ;
/// AppDomain, pour ce qui remonte malgré tout côté .NET ; TaskScheduler, pour une tâche en tâche de fond jamais
/// observée — moins grave, n'aurait pas forcément fait planter l'appli, mais utile à voir quand même).
/// <b>IMPORTANT — jamais testé</b> : écrit d'après l'API documentée, mais sans appareil Android disponible ici pour
/// déclencher un vrai plantage et vérifier que le fichier est bien écrit à temps avant la fermeture du processus.
/// </summary>
public static class CrashLog
{
    private static readonly string FilePath = Path.Combine(FileSystem.AppDataDirectory, "crash.log");
    private static bool _installed;

    public static void Install()
    {
        if (_installed) return;
        _installed = true;

        AndroidEnvironment.UnhandledExceptionRaiser += (_, e) => Write("AndroidEnvironment", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Write("AppDomain", e.ExceptionObject as System.Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => Write("TaskScheduler (tâche de fond)", e.Exception);
    }

    /// <summary>Nombre d'entrées conservées (la plus récente en premier).</summary>
    private const int MaxEntries = 5;

    /// <summary>Consigne une exception rattrapée ailleurs dans l'app (pas forcément un plantage complet) — même
    /// fichier/format que les plantages non rattrapés installés par <see cref="Install"/>.
    /// <para>
    /// v1.34.22sexies : garde désormais un petit HISTORIQUE (les <see cref="MaxEntries"/> dernières entrées) au lieu
    /// d'écraser à chaque appel — en débogant le plantage ForegroundServiceDidNotStartInTimeException, on a réalisé
    /// qu'un appel rattrapé par notre propre try/catch dans OnStartCommand pouvait être écrasé une fraction de
    /// seconde plus tard par l'exception du « chien de garde » système (lancée en AppDomain, un peu après), ce qui
    /// cachait la vraie cause initiale. Avec l'historique, les deux restent visibles, dans l'ordre.
    /// </para>
    /// </summary>
    internal static void Write(string source, System.Exception? ex)
    {
        if (ex is null) return;
        AppendEntry($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} — {source}\n{ex}\n");
    }

    /// <summary>
    /// v1.34.22sexies : marqueur de progression SANS exception — pour savoir, en cas de plantage, jusqu'où le code a
    /// pu s'exécuter avant (ex. "juste avant StartForeground", "StartForeground a réussi"). Utilisé ponctuellement
    /// pour diagnostiquer ForegroundServiceDidNotStartInTimeException, dont la vraie cause reste invisible avec
    /// seulement l'exception finale du système.
    /// </summary>
    internal static void WriteMarker(string message)
        => AppendEntry($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} — [marqueur] {message}\n");

    private static void AppendEntry(string entry)
    {
        try
        {
            var previous = File.Exists(FilePath) ? File.ReadAllText(FilePath) : "";
            var entries = previous.Length == 0
                ? new List<string>()
                : previous.Split(EntrySeparator, StringSplitOptions.RemoveEmptyEntries).ToList();
            entries.Insert(0, entry);
            if (entries.Count > MaxEntries) entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
            File.WriteAllText(FilePath, string.Join(EntrySeparator, entries));
        }
        catch (System.Exception) { /* rien de mieux à faire si même l'écriture du journal échoue */ }
    }

    private const string EntrySeparator = "\n========================================\n";

    public static string? ReadLast()
    {
        try
        {
            return File.Exists(FilePath) ? File.ReadAllText(FilePath) : null;
        }
        catch (System.Exception)
        {
            return null;
        }
    }

    public static void Clear()
    {
        try { if (File.Exists(FilePath)) File.Delete(FilePath); }
        catch (System.Exception) { /* sans gravité */ }
    }
}

public class AndroidCrashLogProvider : ICrashLogProvider
{
    public bool CanRead => true;
    public string? ReadLast() => CrashLog.ReadLast();
    public void Clear() => CrashLog.Clear();
}
