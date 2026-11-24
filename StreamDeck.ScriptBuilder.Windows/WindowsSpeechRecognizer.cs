using System.Speech.Recognition;
using StreamDeck.ScriptBuilder.Services;

namespace StreamDeck.ScriptBuilderApp;

/// <summary>
/// v1.34.18, demandé explicitement ("côtés pc permettre l'enregistrement aussi") : reconnaissance vocale ponctuelle
/// pour le bouton « Enregistrer un trigger » (ButtonEditor/OptionEditor, réutilisés tels quels dans l'onglet
/// « Téléphone » de Script Builder) — dicte une phrase courte au micro du PC et la renvoie en texte, exactement
/// comme <c>IAppPlatform.RecognizeSpeechOnceAsync</c> côté téléphone. Utilise le moteur de dictée de Windows
/// (<c>System.Speech.Recognition</c>, package NuGet "System.Speech") plutôt qu'un service en ligne : fonctionne
/// hors connexion, mais dépend du moteur de reconnaissance vocale installé par Windows pour la langue du système
/// (normalement présent par défaut) — une nouvelle instance de moteur à chaque appel plutôt qu'un moteur gardé en
/// mémoire : plus simple, et cette fonctionnalité n'est utilisée que ponctuellement (pas en continu comme côté
/// téléphone, voir <c>SetVoiceListeningAsync</c>, jamais repris ici).
///
/// <para><b>Non vérifié dans cet environnement</b> : pas de microphone/moteur de reconnaissance Windows disponible
/// ici pour tester un vrai enregistrement. Écrit d'après l'API publique documentée de <c>System.Speech.Recognition</c>,
/// stable depuis longtemps sur .NET, mais jamais exécutée ici pour de vrai.</para>
/// </summary>
internal sealed class WindowsSpeechRecognizer : IPcSpeechRecognizer
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);

    public bool Available => true;

    public async Task<string?> RecognizeOnceAsync(CancellationToken ct = default)
    {
        try
        {
            using var engine = new SpeechRecognitionEngine();
            engine.LoadGrammar(new DictationGrammar());
            engine.SetInputToDefaultAudioDevice();

            var tcs = new TaskCompletionSource<string?>();
            engine.SpeechRecognized += (_, e) => tcs.TrySetResult(e.Result?.Text);
            engine.RecognizeCompleted += (_, e) => tcs.TrySetResult(null); // rien reconnu (silence, timeout interne du moteur...) : pas d'exception à remonter

            engine.RecognizeAsync(RecognizeMode.Single);

            using var registration = ct.Register(() => tcs.TrySetResult(null));
            var finished = await Task.WhenAny(tcs.Task, Task.Delay(Timeout, CancellationToken.None));

            try { engine.RecognizeAsyncStop(); } catch (Exception) { /* déjà arrêté par RecognizeMode.Single */ }

            return finished == tcs.Task ? await tcs.Task : null;
        }
        catch (Exception)
        {
            // Pas de micro, pas de moteur de reconnaissance installé pour la langue du système, permission refusée…
            // — comme côté téléphone (RecognizeSpeechOnceAsync), on renvoie simplement "rien entendu" plutôt que de
            // faire planter l'écran d'édition.
            return null;
        }
    }
}
