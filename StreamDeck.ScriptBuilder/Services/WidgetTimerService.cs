using StreamDeck.Models;
using StreamDeck.Services;

namespace StreamDeck.ScriptBuilder.Services;

/// <summary>
/// Minuteur/chronomètre pour les touches du widget de bureau (v1.36) — équivalent de TimerService (StreamDeck.Shared,
/// app téléphone/Raspberry), mais exécution 100% locale comme le reste du widget : un son de type "Script" lance le
/// script directement via <see cref="ScriptTester"/> (nom résolu via <see cref="ScriptStore"/>), pas de
/// PcProfile/ActionService impliqué (le widget tourne déjà sur le PC concerné).
/// </summary>
public class WidgetTimerService(ScriptStore store, ScriptTester tester, IAlarmPlayer alarm)
{
    public TimerRunState? Current { get; private set; }

    /// <summary>Id de la touche (WidgetPin.Id) qui a démarré le minuteur courant — v1.36.1, "le timer remplace le
    /// nom du bouton sur le bouton" : permet à Widget.razor de savoir SUR QUELLE touche afficher le compte, et de
    /// basculer démarrer/arrêter au clic sur la même touche.</summary>
    public string? OriginPinId { get; private set; }

    public event Action? Changed;

    public bool IsRunningFor(string pinId) => Current is not null && OriginPinId == pinId;

    public void Start(TimerConfig timerConfig, string label, string originPinId)
    {
        Current?.Dispose();
        alarm.Stop();

        var state = new TimerRunState(timerConfig, label);
        state.Changed += () => Changed?.Invoke();
        state.PhaseEnded += OnPhaseEnded;
        state.Finished += () => Changed?.Invoke();

        Current = state;
        OriginPinId = originPinId;
        Changed?.Invoke();
    }

    private void OnPhaseEnded(TimerPhase phase)
    {
        switch (phase.SoundKind)
        {
            case TimerSoundKind.Alarm:
                alarm.PlayAlarm();
                break;

            case TimerSoundKind.Script:
                if (!string.IsNullOrWhiteSpace(phase.SoundScript))
                {
                    var info = store.List().FirstOrDefault(s => s.Id.Equals(phase.SoundScript, StringComparison.OrdinalIgnoreCase));
                    if (info is not null) _ = tester.RunAsync(info.Path);
                }
                break;
        }
    }

    public void ConfirmPending(int seconds) => Current?.ConfirmPending(seconds);

    public void TogglePause() => Current?.TogglePause();

    public void SkipPhase() => Current?.SkipPhase();

    public void Stop()
    {
        alarm.Stop();
        Current?.Dispose();
        Current = null;
        OriginPinId = null;
        Changed?.Invoke();
    }
}
