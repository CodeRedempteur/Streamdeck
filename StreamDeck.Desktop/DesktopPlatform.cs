using StreamDeck.Services;

namespace StreamDeck.Desktop;

internal sealed class DesktopPlatform : IAppPlatform
{
    // Linux : ~/.config/StreamDeck   |   Windows : %APPDATA%\StreamDeck
    public string ConfigDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StreamDeck");

    public bool CanQuit => true;

    public void Quit() => Environment.Exit(0);

    // Pas de vibration sur un PC ou un Raspberry Pi.
    public bool CanVibrate => false;
    public bool CanListen => false;

    public Task<bool> SetVoiceListeningAsync(bool enabled) => Task.FromResult(false);

    public Task<string?> RecognizeSpeechOnceAsync() => Task.FromResult<string?>(null);

    public void Vibrate()
    {
    }

    // "Retrouver le téléphone" n'a pas de sens sur un PC ou un Raspberry Pi : pas de haut-parleur à faire sonner
    // pour se localiser soi-même. No-op délibéré — l'utilisateur n'a demandé ça que pour le téléphone.
    public bool CanFindPhone => false;

    public void TriggerFindPhone(int autoStopSeconds, Action onAutoStopped, string? ringtonePath = null)
    {
    }

    public void StopFindPhone()
    {
    }

    // Même raisonnement que "Retrouver le téléphone" ci-dessus : les sonneries personnalisées n'ont de sens que pour
    // "Retrouver mon téléphone" sur Android, pas ici.
    public bool CanPickAudioFile => false;

    public Task<string?> PickAudioFileAsync() => Task.FromResult<string?>(null);

    public string RingtoneFilePath(string fileName) => Path.Combine(ConfigDirectory, "Ringtones", fileName);

    // Recevoir des commandes du PC n'a de sens que sur le téléphone (c'est justement le PC qui les envoie) — no-op
    // délibéré ici, comme pour "Retrouver le téléphone" plus haut.
    public bool CanRunPhoneServer => false;

    public Task<bool> SetPhoneServerAsync(bool enabled) => Task.FromResult(false);

    public string? LocalIpAddress => null;
}
