namespace StreamDeck.Services;

public record HistoryEntry(DateTime Time, string Workspace, string Label, string Message, string Kind);

/// <summary>Derniers appuis avec leur résultat (en mémoire, effacé à la fermeture de l'app).</summary>
public class HistoryService
{
    private const int MaxEntries = 100;

    private readonly object _lock = new();
    private readonly List<HistoryEntry> _entries = new();

    public event Action? Changed;

    /// <summary>Du plus récent au plus ancien.</summary>
    public IReadOnlyList<HistoryEntry> Entries
    {
        get
        {
            lock (_lock) return _entries.ToList();
        }
    }

    public void Add(string workspace, string label, string message, string kind)
    {
        lock (_lock)
        {
            _entries.Insert(0, new HistoryEntry(DateTime.Now, workspace, label, message, kind));
            if (_entries.Count > MaxEntries)
                _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
        }

        Changed?.Invoke();
    }

    public void Clear()
    {
        lock (_lock) _entries.Clear();
        Changed?.Invoke();
    }
}
