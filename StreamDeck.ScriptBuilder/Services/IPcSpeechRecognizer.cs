namespace StreamDeck.ScriptBuilder.Services;

/// <summary>
/// v1.34.18, demandé explicitement ("côtés pc permettre l'enregistrement aussi") : reconnaissance vocale ponctuelle
/// (une phrase, puis stop) pour enregistrer un trigger vocal de bouton depuis le PC — même rôle que
/// <c>IAppPlatform.RecognizeSpeechOnceAsync</c> côté téléphone, mais une interface à part ici : ce projet
/// (StreamDeck.ScriptBuilder) cible <c>net10.0</c> (multi-plateforme), alors que la vraie implémentation a besoin
/// d'API Windows (<c>System.Speech.Recognition</c>) et ne peut donc vivre que dans l'hôte Windows
/// (StreamDeck.ScriptBuilder.Windows, voir WindowsSpeechRecognizer) — enregistré par-dessus le
/// <see cref="NoOpSpeechRecognizer"/> par défaut (voir ServiceRegistration.AddScriptBuilder, TryAddSingleton).
/// </summary>
public interface IPcSpeechRecognizer
{
    /// <summary>Faux si aucune implémentation réelle n'est enregistrée pour cet hôte (voir <see cref="NoOpSpeechRecognizer"/>) — masque le bouton micro plutôt que de le montrer pour qu'il échoue silencieusement.</summary>
    bool Available { get; }

    /// <summary>Écoute une seule phrase courte et renvoie exactement ce qui a été reconnu, ou null si rien n'a été compris/annulé/non disponible.</summary>
    Task<string?> RecognizeOnceAsync(CancellationToken ct = default);
}

/// <summary>Reconnaissance vocale désactivée — hôte sans implémentation réelle (voir <see cref="IPcSpeechRecognizer"/>).</summary>
public class NoOpSpeechRecognizer : IPcSpeechRecognizer
{
    public bool Available => false;
    public Task<string?> RecognizeOnceAsync(CancellationToken ct = default) => Task.FromResult<string?>(null);
}
