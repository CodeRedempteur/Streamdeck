using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Speech;
using AndroidX.Core.App;
using StreamDeck.Models;
using StreamDeck.Services;

namespace StreamDeck.Mobile;

/// <summary>
/// Écoute continue en tâche de fond : mot de réveil, puis commande (voir <see cref="VoiceMatcher"/> côté partagé,
/// qui décide de tout — ce fichier ne fait que transmettre le texte reconnu). Service au premier plan, obligatoire
/// sur Android pour garder le micro ouvert hors de l'app : une notification permanente reste affichée tant que
/// l'écoute est active (c'est Android qui l'exige, pas un choix — impossible de l'enlever sans arrêter l'écoute).
///
/// <para>
/// <b>IMPORTANT — jamais testé.</b> Aucun appareil ni émulateur Android n'était disponible pendant l'écriture de ce
/// fichier ; il n'a donc jamais tourné une seule fois. Écrit en suivant la documentation officielle d'Android
/// (<c>SpeechRecognizer</c>, service au premier plan de type microphone), mais plusieurs points ne peuvent être
/// vérifiés qu'en conditions réelles :
/// </para>
/// <list type="bullet">
/// <item>La fiabilité de l'écoute VRAIMENT continue : <c>SpeechRecognizer</c> ne reconnaît qu'un seul énoncé par
/// appel à <c>StartListening</c> ; ce service le relance après chaque résultat ou erreur, mais le micro attend
/// peut-être un court instant entre deux tentatives (paroles éventuellement manquées pendant ce trou).</item>
/// <item>Certains fabricants (Xiaomi, Huawei, Samsung selon les réglages) tuent les services en tâche de fond
/// malgré le statut « premier plan », sauf à désactiver manuellement l'optimisation de batterie pour l'app.</item>
/// <item>Le moteur de reconnaissance vocale de Google peut nécessiter une connexion réseau selon l'appareil, et
/// être soumis à des quotas d'utilisation invisibles d'ici.</item>
/// </list>
/// </summary>
// v1.34.24 : voir le commentaire équivalent dans PhoneCommandServerService.cs — ForegroundServiceType doit être
// déclaré ici, sur l'attribut [Service], pas (seulement) dans AndroidManifest.xml.
[Service(Exported = false, ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeMicrophone)]
public class VoiceListenerService : Service, IRecognitionListener
{
    private const string ChannelId = "streamdeck-voice";
    private const int NotificationId = 4201;

    /// <summary>Délai avant de relancer l'écoute après un résultat ou un silence : évite d'enchaîner en boucle serrée.</summary>
    private const int RestartDelayMs = 350;

    /// <summary>Délai plus long après une vraie erreur (micro occupé, réseau...) : laisse un peu de répit.</summary>
    private const int ErrorBackoffMs = 2500;

    private SpeechRecognizer? _recognizer;
    private Handler? _handler;
    private VoiceMatcher? _matcher;
    private ActionService? _actions;
    private ConfigService? _config;
    private HistoryService? _history;
    private VoiceHistoryService? _voiceHistory;
    private bool _stopping;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        // v1.34.22 : même protection + même correctif que PhoneCommandServerService.OnStartCommand (voir son
        // commentaire — MissingForegroundServiceTypeException confirmée par un journal de plantage réel pour ce
        // service-ci ; appliquée ici aussi par précaution, même principe de démarrage de service au premier plan).
        try
        {
            _stopping = false;
            _handler = new Handler(Looper.MainLooper!);

            // v1.34.22quinquies : voir le commentaire équivalent dans PhoneCommandServerService.cs —
            // ForegroundServiceDidNotStartInTimeException observée sur Samsung. StartForeground() en tout premier,
            // avant de résoudre les autres services, pour laisser le moins de travail possible avant cet appel.
            global::AndroidX.Core.App.ServiceCompat.StartForeground(this, NotificationId, BuildNotification(),
                (int)global::Android.Content.PM.ForegroundService.TypeMicrophone);

            // Les mêmes instances que celles utilisées par l'interface Blazor (config, historique, exécution des
            // boutons) : le service et l'appli restent cohérents (un bouton lancé à la voix apparaît dans l'historique).
            var services = MainApplication.CurrentServices;
            _matcher = services?.GetService(typeof(VoiceMatcher)) as VoiceMatcher;
            _actions = services?.GetService(typeof(ActionService)) as ActionService;
            _config = services?.GetService(typeof(ConfigService)) as ConfigService;
            _history = services?.GetService(typeof(HistoryService)) as HistoryService;
            _voiceHistory = services?.GetService(typeof(VoiceHistoryService)) as VoiceHistoryService;

            StartRecognizer();
        }
        catch (System.Exception ex)
        {
            CrashLog.Write("VoiceListenerService.OnStartCommand", ex);
            StopSelf();
        }

        // Sticky : si le système tue le service pour libérer de la mémoire, il tente de le relancer plus tard.
        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        _stopping = true;
        _handler?.RemoveCallbacksAndMessages(null);
        try { _recognizer?.StopListening(); _recognizer?.Destroy(); } catch (System.Exception) { /* déjà arrêté */ }
        _recognizer = null;
        base.OnDestroy();
    }

    /// <summary>(Re)lance une session d'écoute. Une session ne dure qu'un seul énoncé : il faut la relancer après chaque résultat.</summary>
    private void StartRecognizer()
    {
        if (_stopping) return;
        if (_config?.Current.Voice.Enabled != true) { StopSelf(); return; }

        try
        {
            _recognizer?.Destroy();
            _recognizer = SpeechRecognizer.CreateSpeechRecognizer(this);
            _recognizer.SetRecognitionListener(this);

            var intent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
            intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
            intent.PutExtra(RecognizerIntent.ExtraLanguage, Java.Util.Locale.Default);
            intent.PutExtra(RecognizerIntent.ExtraPartialResults, false);
            intent.PutExtra(RecognizerIntent.ExtraMaxResults, 1);
            intent.PutExtra(RecognizerIntent.ExtraCallingPackage, PackageName);

            _recognizer.StartListening(intent);
        }
        catch (System.Exception)
        {
            // Micro momentanément indisponible (appel en cours, autre appli qui l'utilise...) : on retentera plus tard.
            ScheduleRestart(ErrorBackoffMs);
        }
    }

    private void ScheduleRestart(int delayMs)
    {
        if (_stopping) return;
        _handler?.PostDelayed(StartRecognizer, delayMs);
    }

    public void OnResults(Bundle? results)
    {
        var text = results?.GetStringArrayList(SpeechRecognizer.ResultsRecognition)?.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(text)) HandleUtterance(text);
        ScheduleRestart(RestartDelayMs);
    }

    public void OnError(SpeechRecognizerError error)
    {
        // Silence, aucune correspondance, délai dépassé : normal en écoute continue (personne ne parle en
        // permanence) — on relance tout de suite. Les autres erreurs laissent un peu de répit avant de réessayer.
        var delay = error is SpeechRecognizerError.NoMatch or SpeechRecognizerError.SpeechTimeout
            ? RestartDelayMs
            : ErrorBackoffMs;
        ScheduleRestart(delay);
    }

    /// <summary>Transmet la phrase reconnue à VoiceMatcher (logique partagée, testée), consigne toujours dans
    /// l'historique vocal (reconnue ou non), et lance le bouton — ou le choix précis d'un bouton « choix multiple »
    /// — s'il y en a un (consigné dans l'historique des actions à la fin, comme un appui normal).</summary>
    private void HandleUtterance(string text)
    {
        if (_matcher is null || _actions is null || _config is null) return;

        var cfg = _config.Current;
        if (!cfg.Voice.Enabled) return;

        var allButtons = cfg.Workspaces.SelectMany(w => w.Pages).SelectMany(p => p.Buttons);

        // v1.35 : « Retrouver mon téléphone » à la voix est un réglage à part (VoiceSettings.FindPhoneVoiceEnabled),
        // indépendant de tout bouton posé sur une page — on compose ici un bouton FindPhone "virtuel" (jamais
        // affiché nulle part) et on l'ajoute à la liste avant de la transmettre à VoiceMatcher, qui n'a besoin
        // d'aucune autre modification (voir DeckConfig.cs/VoiceSettings pour le détail du choix).
        if (cfg.Voice.FindPhoneVoiceEnabled && !string.IsNullOrWhiteSpace(cfg.Voice.FindPhoneVoiceTrigger))
        {
            var findPhoneButton = new DeckButton
            {
                Kind = ButtonKind.FindPhone,
                Label = "Retrouver le téléphone",
                Enabled = true,
                VoiceTriggers = new List<string> { cfg.Voice.FindPhoneVoiceTrigger },
                VoiceRequiresWakeWord = cfg.Voice.FindPhoneVoiceRequiresWakeWord
            };
            allButtons = allButtons.Append(findPhoneButton);
        }

        var result = _matcher.Process(text, cfg.Voice, allButtons);

        _voiceHistory?.Add(text, result.Label, result.WakeWordHeard);
        if (result.Button is null) return;

        // Exécution "tire et oublie" : le service ne bloque pas l'écoute suivante en attendant la fin du script.
        var workspace = cfg.ActiveWorkspace;
        var button = result.Button;
        var option = result.Option;
        var label = result.Label ?? button.Label;
        _ = Task.Run(async () =>
        {
            try
            {
                var run = option is not null
                    ? await _actions.ExecuteOptionAsync(workspace, button, option)
                    : await _actions.ExecuteAsync(workspace, button);
                var kind = !run.Ok ? "is-err" : run.Warning ? "is-warn" : "is-ok";
                _history?.Add(workspace.Name, $"{label} (voix)", run.Message, kind);
            }
            catch (System.Exception ex)
            {
                _history?.Add(workspace.Name, $"{label} (voix)", ex.Message, "is-err");
            }
        });
    }

    private Notification BuildNotification()
    {
        var manager = (NotificationManager)GetSystemService(NotificationService)!;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O && manager.GetNotificationChannel(ChannelId) is null)
        {
            var channel = new NotificationChannel(ChannelId, "Commande vocale", NotificationImportance.Low)
            {
                Description = "Reste affichée tant que l'écoute vocale de Stream Deck est active."
            };
            manager.CreateNotificationChannel(channel);
        }

        return new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle("Stream Deck écoute")
            .SetContentText("Dis le mot de réveil pour lancer une commande.")
            .SetSmallIcon(ApplicationInfo!.Icon)
            .SetOngoing(true)
            .SetPriority(NotificationCompat.PriorityLow)
            .Build()!;
    }

    // ----- IRecognitionListener : le reste ne sert à rien ici -----
    public void OnBeginningOfSpeech() { }
    public void OnBufferReceived(byte[]? buffer) { }
    public void OnEndOfSpeech() { }
    public void OnEvent(int eventType, Bundle? args) { }
    public void OnPartialResults(Bundle? partialResults) { }
    public void OnReadyForSpeech(Bundle? args) { }
    public void OnRmsChanged(float rmsdB) { }

    // Ajoutés par une version plus récente de l'interface Android (RecognitionListener) que la précédente écriture
    // de ce fichier ne connaissait pas encore — sans intérêt ici, on ignore juste ces événements.
    public void OnEndOfSegmentedSession() { }
    public void OnLanguageDetection(Bundle? bundle) { }
    public void OnSegmentResults(Bundle? bundle) { }
}
