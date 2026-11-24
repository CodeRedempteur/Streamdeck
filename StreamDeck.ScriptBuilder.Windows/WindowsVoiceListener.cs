using System.Speech.Recognition;
using StreamDeck.ScriptBuilder.Services;

namespace StreamDeck.ScriptBuilderApp;

/// <summary>
/// v1.35, demandé explicitement ("autorisation de trigger ces bouton par la voix depuis le pc") : écoute vocale
/// CONTINUE du widget (mot de réveil puis déclencheur d'une touche épinglée, voir <c>BuilderConfig.WidgetVoice</c>
/// et <c>Widget.razor</c>) — moteur de dictée de Windows (<c>System.Speech.Recognition</c>, même package que
/// <see cref="WindowsSpeechRecognizer"/>), mais un SEUL moteur gardé ouvert en mode continu
/// (<see cref="RecognizeMode.Multiple"/>) plutôt qu'une nouvelle instance à chaque phrase — nécessaire pour une
/// écoute qui dure tant que le widget est ouvert, pas une seule dictée ponctuelle.
///
/// <para><b>Non vérifié dans cet environnement</b> : comme <see cref="WindowsSpeechRecognizer"/>, pas de
/// microphone/moteur de reconnaissance Windows disponible ici pour tester une vraie écoute continue. Écrit d'après
/// l'API publique documentée de <c>System.Speech.Recognition</c> (évènement <c>SpeechRecognized</c>, répété tant que
/// <c>RecognizeAsync(RecognizeMode.Multiple)</c> tourne), jamais exécutée ici pour de vrai.</para>
/// </summary>
internal sealed class WindowsVoiceListener : IPcVoiceListener, IDisposable
{
    private SpeechRecognitionEngine? _engine;
    private Action<string>? _onUtterance;

    public bool Available => true;
    public bool Listening { get; private set; }

    public void Start(Action<string> onUtterance)
    {
        if (Listening) return;

        try
        {
            _onUtterance = onUtterance;

            var engine = new SpeechRecognitionEngine();
            engine.SetInputToDefaultAudioDevice();
            engine.LoadGrammar(new DictationGrammar());
            engine.SpeechRecognized += OnSpeechRecognized;
            engine.RecognizeAsync(RecognizeMode.Multiple);

            _engine = engine;
            Listening = true;
        }
        catch (Exception)
        {
            // Pas de micro, pas de moteur de reconnaissance installé pour la langue du système, permission refusée…
            // — l'écoute reste simplement inactive (Listening = false), le widget continue de fonctionner sans elle.
            Stop();
        }
    }

    private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
    {
        var text = e.Result?.Text;
        if (!string.IsNullOrWhiteSpace(text)) _onUtterance?.Invoke(text);
    }

    public void Stop()
    {
        var engine = _engine;
        _engine = null;
        Listening = false;
        if (engine is null) return;

        try
        {
            engine.SpeechRecognized -= OnSpeechRecognized;
            engine.RecognizeAsyncStop();
            engine.Dispose();
        }
        catch (Exception) { /* déjà arrêté/libéré */ }
    }

    public void Dispose() => Stop();
}
