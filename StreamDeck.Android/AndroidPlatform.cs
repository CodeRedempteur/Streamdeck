using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using StreamDeck.Services;

namespace StreamDeck.Mobile;

internal sealed class AndroidPlatform : IAppPlatform
{
    // Stockage privé de l'application (inaccessible aux autres applis).
    public string ConfigDirectory => FileSystem.AppDataDirectory;

    // Sur Android, on quitte avec le geste/bouton système.
    public bool CanQuit => false;

    public void Quit()
    {
    }

    public bool CanVibrate => Vibration.Default.IsSupported;

    public bool CanListen => true;

    /// <summary>
    /// Demande la permission micro (si besoin) puis démarre/arrête le service d'écoute continue.
    /// NOTE (développement) : ce chemin — permission, service au premier plan, SpeechRecognizer — n'a jamais pu
    /// être testé en conditions réelles ici (pas d'appareil/émulateur Android disponible). Écrit en suivant la
    /// documentation officielle ; à vérifier sur un vrai appareil avant de compter dessus.
    /// </summary>
    public async Task<bool> SetVoiceListeningAsync(bool enabled)
    {
        var context = Android.App.Application.Context;

        if (!enabled)
        {
            context.StopService(new Android.Content.Intent(context, typeof(VoiceListenerService)));
            return true;
        }

        var status = await Permissions.RequestAsync<Permissions.Microphone>();
        if (status != PermissionStatus.Granted) return false;

        var intent = new Android.Content.Intent(context, typeof(VoiceListenerService));
        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
            context.StartForegroundService(intent);
        else
            context.StartService(intent);

        return true;
    }

    // Courte vibration de retour au toucher (permission VIBRATE dans le manifeste).
    public void Vibrate()
    {
        try
        {
            if (Vibration.Default.IsSupported)
                Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(18));
        }
        catch (System.Exception)
        {
            // Vibreur indisponible ou refusé : sans gravité.
        }
    }

    /// <summary>
    /// Écoute une seule phrase (jusqu'à 8 s), pour enregistrer une phrase déclencheuse telle que la reconnaissance
    /// vocale l'entend vraiment, plutôt que de la deviner en la tapant.
    /// NOTE (développement) : comme <see cref="SetVoiceListeningAsync"/>, jamais testée sur un appareil réel.
    /// </summary>
    public async Task<string?> RecognizeSpeechOnceAsync()
    {
        var status = await Permissions.RequestAsync<Permissions.Microphone>();
        if (status != PermissionStatus.Granted) return null;

        var context = Android.App.Application.Context;
        if (!Android.Speech.SpeechRecognizer.IsRecognitionAvailable(context)) return null;

        var listener = new OneShotRecognitionListener();
        var recognizer = Android.Speech.SpeechRecognizer.CreateSpeechRecognizer(context);
        recognizer.SetRecognitionListener(listener);

        try
        {
            var intent = new Android.Content.Intent(Android.Speech.RecognizerIntent.ActionRecognizeSpeech);
            intent.PutExtra(Android.Speech.RecognizerIntent.ExtraLanguageModel, Android.Speech.RecognizerIntent.LanguageModelFreeForm);
            intent.PutExtra(Android.Speech.RecognizerIntent.ExtraLanguage, Java.Util.Locale.Default);
            intent.PutExtra(Android.Speech.RecognizerIntent.ExtraMaxResults, 1);

            recognizer.StartListening(intent);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            using var reg = cts.Token.Register(() => listener.Result.TrySetResult(null));
            return await listener.Result.Task;
        }
        finally
        {
            try { recognizer.StopListening(); recognizer.Destroy(); }
            catch (System.Exception) { /* déjà arrêté */ }
        }
    }

    /// <summary>Pont entre les rappels Android (callbacks) et une seule <see cref="Task"/> attendue par <see cref="RecognizeSpeechOnceAsync"/>.</summary>
    private sealed class OneShotRecognitionListener : Java.Lang.Object, Android.Speech.IRecognitionListener
    {
        public TaskCompletionSource<string?> Result { get; } = new();

        public void OnResults(Android.OS.Bundle? results)
        {
            var text = results?.GetStringArrayList(Android.Speech.SpeechRecognizer.ResultsRecognition)?.FirstOrDefault();
            Result.TrySetResult(string.IsNullOrWhiteSpace(text) ? null : text);
        }

        public void OnError(Android.Speech.SpeechRecognizerError error) => Result.TrySetResult(null);

        public void OnBeginningOfSpeech() { }
        public void OnBufferReceived(byte[]? buffer) { }
        public void OnEndOfSpeech() { }
        public void OnEvent(int eventType, Android.OS.Bundle? args) { }
        public void OnPartialResults(Android.OS.Bundle? partialResults) { }
        public void OnReadyForSpeech(Android.OS.Bundle? args) { }
        public void OnRmsChanged(float rmsdB) { }

        // Ajoutés par une version plus récente de l'interface Android que la première écriture de ce fichier ne
        // connaissait pas encore (voir VoiceListenerService.cs) — sans intérêt ici, on ignore juste ces événements.
        public void OnEndOfSegmentedSession() { }
        public void OnLanguageDetection(Android.OS.Bundle? bundle) { }
        public void OnSegmentResults(Android.OS.Bundle? bundle) { }
    }

    // ----- Retrouver le téléphone (v1.35) -----

    public bool CanFindPhone => true;

    private Android.Media.MediaPlayer? _findPhonePlayer;
    private Android.OS.Handler? _findPhoneHandler;
    private Action? _findPhoneAutoStop;

    /// <summary>
    /// NOTE (développement) : comme <see cref="SetVoiceListeningAsync"/>, jamais testé sur un appareil réel (pas
    /// d'appareil/émulateur Android disponible dans cet environnement). Écrit d'après l'API Android documentée
    /// (<c>AudioManager</c>, <c>RingtoneManager</c>/<c>MediaPlayer</c>), avec deux réserves à vérifier en conditions
    /// réelles :
    /// <list type="bullet">
    /// <item>Forcer <c>RingerMode = Normal</c> exige, depuis Android 7 (Nougat), l'autorisation "Accès aux règles Ne
    /// pas déranger" (<c>ACCESS_NOTIFICATION_POLICY</c>) — accordée à la main par l'utilisateur dans les réglages
    /// système, impossible à demander via une simple boîte de dialogue (contrairement à <c>Permissions.RequestAsync</c>
    /// utilisé pour le micro). Si elle n'est pas accordée, le changement de mode échoue silencieusement (try/catch
    /// ci-dessous) : seule l'alarme jouée sur le flux <c>Alarm</c> — qui contourne le mode silencieux sur la plupart
    /// des appareils — reste fiable pour retrouver le téléphone.</item>
    /// <item>Le comportement de <c>RingtoneManager</c> et du flux <c>Alarm</c> peut varier d'un fabricant à l'autre
    /// (Xiaomi, Huawei, Samsung...), comme pour l'écoute vocale continue (voir <c>VoiceListenerService</c>).</item>
    /// </list>
    /// </summary>
    /// <param name="ringtonePath">v1.35 : chemin complet d'un fichier audio local (voir PickAudioFileAsync/RingtoneFilePath) — si fourni et lisible, remplace la sonnerie d'alarme par défaut ci-dessous.</param>
    public void TriggerFindPhone(int autoStopSeconds, Action onAutoStopped, string? ringtonePath = null)
    {
        try
        {
            StopFindPhone();

            var context = Android.App.Application.Context;
            var audio = (Android.Media.AudioManager)context.GetSystemService(Android.Content.Context.AudioService)!;

            try
            {
                audio.RingerMode = Android.Media.RingerMode.Normal;
            }
            catch (System.Exception)
            {
                // Autorisation "Ne pas déranger" non accordée : sans gravité, voir la remarque ci-dessus.
            }

            try
            {
                audio.SetStreamVolume(Android.Media.Stream.Alarm, audio.GetStreamMaxVolume(Android.Media.Stream.Alarm), Android.Media.VolumeNotificationFlags.PlaySound);
                // VolumeNotificationFlags n'a pas de membre "None" (énum [Flags] Android) : 0 = aucun indicateur, silencieusement.
                audio.SetStreamVolume(Android.Media.Stream.Ring, audio.GetStreamMaxVolume(Android.Media.Stream.Ring), (Android.Media.VolumeNotificationFlags)0);
            }
            catch (System.Exception)
            {
                // Réglage du volume refusé selon l'appareil : sans gravité, l'alarme joue quand même.
            }

            // v1.35 : sonnerie personnalisée (fichier local) si fournie et encore présente sur le disque, sinon
            // repli sur la sonnerie d'alarme par défaut de l'appareil (comportement d'origine, inchangé).
            var useCustomFile = !string.IsNullOrWhiteSpace(ringtonePath) && File.Exists(ringtonePath);
            var uri = useCustomFile ? null : Android.Media.RingtoneManager.GetActualDefaultRingtoneUri(context, Android.Media.RingtoneType.Alarm)
                                              ?? Android.Media.RingtoneManager.GetDefaultUri(Android.Media.RingtoneType.Alarm);

            if (useCustomFile || uri is not null)
            {
                var player = new Android.Media.MediaPlayer();
                player.SetAudioStreamType(Android.Media.Stream.Alarm);
                if (useCustomFile) player.SetDataSource(ringtonePath);
                else player.SetDataSource(context, uri!);
                player.Looping = true;
                player.Prepare();
                player.Start();
                _findPhonePlayer = player;
            }

            if (Vibration.Default.IsSupported)
                Vibration.Default.Vibrate(TimeSpan.FromSeconds(System.Math.Max(1, autoStopSeconds)));

            _findPhoneHandler = new Android.OS.Handler(Android.OS.Looper.MainLooper!);
            _findPhoneAutoStop = () =>
            {
                StopFindPhone();
                onAutoStopped();
            };
            _findPhoneHandler.PostDelayed(_findPhoneAutoStop, (long)TimeSpan.FromSeconds(System.Math.Max(1, autoStopSeconds)).TotalMilliseconds);
        }
        catch (System.Exception)
        {
            // Filet de sécurité : ne doit jamais faire planter l'appli (voir ActionService/FindPhoneService).
        }
    }

    public void StopFindPhone()
    {
        try
        {
            if (_findPhoneAutoStop is not null)
            {
                _findPhoneHandler?.RemoveCallbacks(_findPhoneAutoStop);
                _findPhoneAutoStop = null;
            }

            if (_findPhonePlayer is not null)
            {
                _findPhonePlayer.Stop();
                _findPhonePlayer.Release();
                _findPhonePlayer = null;
            }

            Vibration.Default.Cancel();
        }
        catch (System.Exception)
        {
            // Déjà arrêté, ou lecteur dans un état inattendu : sans gravité.
        }
    }

    // ----- Sonneries personnalisées (v1.35) -----

    public bool CanPickAudioFile => true;

    /// <summary>
    /// NOTE (développement) : comme le reste de ce fichier, jamais testé sur un appareil réel. Utilise le
    /// sélecteur de fichiers générique de MAUI (<c>FilePicker.Default.PickAsync</c>) sans filtre de type
    /// restrictif (volontaire : mieux vaut laisser l'utilisateur choisir large que risquer un mauvais nom de
    /// membre sur <c>FilePickerFileType</c> qui empêcherait tout choix). Copie le fichier choisi dans le
    /// sous-dossier "Ringtones" du dossier de config de l'app, en dédupliquant le nom si besoin.
    /// </summary>
    public async Task<string?> PickAudioFileAsync()
    {
        try
        {
            var picked = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choisir un fichier audio (mp3...)"
            });
            if (picked is null) return null;

            var dir = Path.Combine(ConfigDirectory, "Ringtones");
            Directory.CreateDirectory(dir);

            var fileName = Path.GetFileName(picked.FileName);
            if (string.IsNullOrWhiteSpace(fileName)) fileName = $"sonnerie-{Guid.NewGuid():N}.mp3";

            var destPath = Path.Combine(dir, fileName);
            if (File.Exists(destPath))
            {
                // Évite d'écraser un fichier existant du même nom : ajoute un suffixe unique.
                var ext = Path.GetExtension(fileName);
                var baseName = Path.GetFileNameWithoutExtension(fileName);
                fileName = $"{baseName}-{Guid.NewGuid():N}{ext}";
                destPath = Path.Combine(dir, fileName);
            }

            await using var source = await picked.OpenReadAsync();
            await using var dest = File.Create(destPath);
            await source.CopyToAsync(dest);

            return fileName;
        }
        catch (System.Exception)
        {
            return null;
        }
    }

    public string RingtoneFilePath(string fileName) => Path.Combine(ConfigDirectory, "Ringtones", fileName);

    // ----- Serveur de commandes PC → téléphone (v1.34.13) -----

    public bool CanRunPhoneServer => true;

    /// <summary>NOTE (développement) : comme le reste de ce fichier, jamais testé sur un appareil réel.</summary>
    public Task<bool> SetPhoneServerAsync(bool enabled)
    {
        var context = Android.App.Application.Context;

        if (!enabled)
        {
            context.StopService(new Android.Content.Intent(context, typeof(PhoneCommandServerService)));
            return Task.FromResult(true);
        }

        var intent = new Android.Content.Intent(context, typeof(PhoneCommandServerService));
        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
            context.StartForegroundService(intent);
        else
            context.StartService(intent);

        return Task.FromResult(true);
    }

    /// <summary>Meilleure estimation, pas une garantie (plusieurs interfaces actives possibles) — affichée côté
    /// Réglages pour que l'utilisateur la recopie dans Script Builder, pas utilisée ailleurs dans le code.</summary>
    public string? LocalIpAddress
    {
        get
        {
            try
            {
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;

                    foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(addr.Address))
                            return addr.Address.ToString();
                    }
                }
            }
            catch (System.Exception)
            {
                // Liste d'interfaces illisible selon l'appareil : tant pis, affichage simplement vide côté Réglages.
            }
            return null;
        }
    }
}
