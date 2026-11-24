namespace StreamDeck.Services;

/// <summary>Une phrase entendue par la commande vocale, avec ce qu'elle a déclenché (ou pas).</summary>
public record VoiceHistoryEntry(DateTime Time, string Utterance, string? Button, bool WakeWordHeard)
{
    public bool Matched => Button is not null;
}

/// <summary>Dernières phrases entendues par la commande vocale, qu'elles aient déclenché un bouton ou non
/// (en mémoire, effacé à la fermeture de l'app — comme <see cref="HistoryService"/>, dont c'est le pendant vocal).</summary>
public class VoiceHistoryService
{
    private const int MaxEntries = 100;

    private readonly object _lock = new();
    private readonly List<VoiceHistoryEntry> _entries = new();

    public event Action? Changed;

    /// <summary>Du plus récent au plus ancien.</summary>
    public IReadOnlyList<VoiceHistoryEntry> Entries
    {
        get
        {
            lock (_lock) return _entries.ToList();
        }
    }

    public void Add(string utterance, string? button, bool wakeWordHeard)
    {
        lock (_lock)
        {
            _entries.Insert(0, new VoiceHistoryEntry(DateTime.Now, utterance, button, wakeWordHeard));
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
