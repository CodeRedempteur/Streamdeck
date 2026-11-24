namespace StreamDeck.Services;

/// <summary>
/// v1.34.20, demandé suite à un plantage signalé sur un Samsung ("sa fait crash lapp... hors sur lautre tel sa
/// marchais") sans détail exploitable (pas d'accès à un PC/adb pour relever le journal Android depuis le téléphone
/// en question) : permet à l'appli de garder elle-même la trace du dernier plantage (texte brut de l'exception) et
/// de l'afficher dans les Réglages pour que tu puisses me le copier-coller directement, sans outil externe.
/// Android uniquement pour l'instant (voir <see cref="NoOpCrashLogProvider"/> ailleurs).
/// </summary>
public interface ICrashLogProvider
{
    /// <summary>true si cette plateforme sait garder une trace des plantages.</summary>
    bool CanRead { get; }

    /// <summary>Texte du dernier plantage enregistré, ou null s'il n'y en a aucun (ou si <see cref="CanRead"/> est faux).</summary>
    string? ReadLast();

    /// <summary>Efface la trace du dernier plantage.</summary>
    void Clear();
}

public class NoOpCrashLogProvider : ICrashLogProvider
{
    public bool CanRead => false;
    public string? ReadLast() => null;
    public void Clear() { }
}
