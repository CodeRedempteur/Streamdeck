namespace StreamDeck.Services;

/// <summary>
/// Joue l'alarme par défaut d'un timer (TimerSoundKind.Alarm) — un service optionnel par plateforme, comme
/// INativeImagePicker/ICrashLogProvider : <see cref="NoOpAlarmPlayer"/> est le repli inactif enregistré par
/// AddStreamDeckCore/AddScriptBuilder (TryAddSingleton), chaque hôte capable de jouer un vrai son enregistre sa
/// propre implémentation AVANT d'appeler cette méthode.
/// </summary>
public interface IAlarmPlayer
{
    /// <summary>Démarre l'alarme, en boucle, jusqu'à <see cref="Stop"/>.</summary>
    void PlayAlarm();

    void Stop();
}

/// <summary>Repli par défaut : ne fait rien (plateforme sans son d'alarme implémenté, ou sandbox).</summary>
public sealed class NoOpAlarmPlayer : IAlarmPlayer
{
    public void PlayAlarm() { }
    public void Stop() { }
}
