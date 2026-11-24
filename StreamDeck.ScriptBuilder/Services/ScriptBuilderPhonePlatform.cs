using StreamDeck.Services;

namespace StreamDeck.ScriptBuilder.Services;

/// <summary>
/// v1.34.16, onglet « Téléphone » (voir PhoneTemplates.razor) : implémentation minimale de <see cref="IAppPlatform"/>
/// pour pouvoir réutiliser SANS LES MODIFIER les composants d'édition partagés avec l'app téléphone
/// (StreamDeck.Shared/Components/Editor/* — WorkspaceSection, PagesSection, ButtonEditor, OptionEditor), demandé
/// explicitement ("l'exact même interface que sur l'app de téléphone"). Tout ce qui est propre au téléphone (micro,
/// vibration, sonnerie « Retrouver le téléphone », petit serveur de commandes…) est désactivé ici : le PC ne sait
/// pas le faire, et ces composants masquent déjà eux-mêmes les boutons correspondants quand la capacité est à faux
/// — rien à adapter dans ces fichiers partagés.
/// </summary>
public class ScriptBuilderPhonePlatform : IAppPlatform
{
    private readonly IPcSpeechRecognizer _speech;

    public ScriptBuilderPhonePlatform(IBuilderPlatform platform, IPcSpeechRecognizer speech)
    {
        // Sous-dossier distinct de celui de BuilderConfigService : les deux services écrivent un "config.json",
        // ils ne doivent pas se marcher dessus. Celui-ci ne sert qu'à satisfaire ConfigService/ButtonEditor
        // (voir leur doc) — jamais affiché, jamais lu pour de vrai côté « Téléphone ».
        ConfigDirectory = Path.Combine(platform.ConfigDirectory, "PhoneEditor");
        _speech = speech;
    }

    public string ConfigDirectory { get; }

    public bool CanQuit => false;
    public void Quit() { }

    public bool CanVibrate => false;
    public void Vibrate() { }

    // v1.34.18, demandé explicitement ("côtés pc permettre l'enregistrement aussi") : délègue à IPcSpeechRecognizer
    // (NoOpSpeechRecognizer par défaut, WindowsSpeechRecognizer sur l'hôte Windows — voir ces fichiers). Seulement
    // le bouton micro en dépend désormais dans ButtonEditor/OptionEditor (v1.34.18) : la saisie au clavier d'un
    // trigger reste possible même quand CanListen est faux.
    public bool CanListen => _speech.Available;
    public Task<bool> SetVoiceListeningAsync(bool enabled) => Task.FromResult(false); // écoute continue (mot de réveil) : concept propre au téléphone, pas repris ici
    public Task<string?> RecognizeSpeechOnceAsync() => _speech.RecognizeOnceAsync();

    public bool CanFindPhone => false;
    public void TriggerFindPhone(int autoStopSeconds, Action onAutoStopped, string? ringtonePath = null) { }
    public void StopFindPhone() { }

    public bool CanPickAudioFile => false;
    public Task<string?> PickAudioFileAsync() => Task.FromResult<string?>(null);
    public string RingtoneFilePath(string fileName) => "";

    public bool CanRunPhoneServer => false;
    public Task<bool> SetPhoneServerAsync(bool enabled) => Task.FromResult(false);

    public string? LocalIpAddress => null;
}
