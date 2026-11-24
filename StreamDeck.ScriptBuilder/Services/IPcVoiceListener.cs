namespace StreamDeck.ScriptBuilder.Services;

/// <summary>
/// v1.35, demandé explicitement ("autorisation de trigger ces bouton par la voix depuis le pc") : écoute vocale
/// CONTINUE côté PC (mot de réveil puis phrase déclencheuse pour une touche épinglée du widget, voir
/// <c>BuilderConfig.WidgetVoice</c>/<c>Widget.razor</c>) — même esprit que <c>VoiceListenerService</c> côté
/// téléphone (Android), mais à ne SURTOUT PAS confondre avec <see cref="IPcSpeechRecognizer"/> ci-dessus : celui-ci
/// ne reconnaît qu'UNE SEULE phrase (pour remplir le champ « Déclencheur vocal » d'une touche dans l'éditeur), alors
/// que celui-ci écoute en continu et déclenche directement une touche dès qu'une phrase correspond.
/// <para>
/// Comme pour <see cref="IPcSpeechRecognizer"/>, ce projet (StreamDeck.ScriptBuilder, net10.0 multi-plateforme) ne
/// définit que l'interface — seul l'hôte Windows (StreamDeck.ScriptBuilder.Windows, voir WindowsVoiceListener) a une
/// vraie implémentation (<c>System.Speech.Recognition</c> en écoute continue), enregistrée par-dessus
/// <see cref="NoOpVoiceListener"/> (voir ServiceRegistration.AddScriptBuilder, TryAddSingleton).
/// </para>
/// </summary>
public interface IPcVoiceListener
{
    /// <summary>Faux si aucune implémentation réelle n'est enregistrée pour cet hôte — masque/désactive le réglage plutôt que de le montrer pour qu'il échoue silencieusement.</summary>
    bool Available { get; }

    /// <summary>Vrai pendant que l'écoute continue tourne (entre <see cref="Start"/> et <see cref="Stop"/>/un échec).</summary>
    bool Listening { get; }

    /// <summary>
    /// Démarre l'écoute continue. <paramref name="onUtterance"/> est appelé pour chaque phrase reconnue (texte brut,
    /// non normalisé) — potentiellement depuis un thread autre que celui de l'UI, à l'appelant de revenir sur le fil
    /// Blazor (<c>InvokeAsync</c>) avant de toucher l'état du composant. Sans effet si déjà en écoute.
    /// </summary>
    void Start(Action<string> onUtterance);

    /// <summary>Arrête l'écoute. Sans effet si elle n'est pas en cours.</summary>
    void Stop();
}

/// <summary>Écoute vocale continue désactivée — hôte sans implémentation réelle (voir <see cref="IPcVoiceListener"/>).</summary>
public class NoOpVoiceListener : IPcVoiceListener
{
    public bool Available => false;
    public bool Listening => false;
    public void Start(Action<string> onUtterance) { }
    public void Stop() { }
}
