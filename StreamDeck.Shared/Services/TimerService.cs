using StreamDeck.Models;

namespace StreamDeck.Services;

/// <summary>
/// Minuteur/chronomètre (v1.36, "possibilité de créer des timer... et sonne... visuel de compte à rebours... mode
/// pomodoro...") — un seul timer actif à la fois. v1.36.1, "le timer remplace le nom du bouton sur le bouton" :
/// affiché directement sur la touche d'origine (voir Home.razor/KeyTile), plus en plein écran (TimerOverlay ne
/// garde que la demande de durée, Pending). Mêmes PC/variables que ActionService (un son de type "Script" lance le
/// script sur le PC du bouton d'origine), pour pouvoir, par exemple, "faire genre Spotify" à la fin d'un décompte.
/// </summary>
public class TimerService(ScriptRunner runner, ConfigService config, IAlarmPlayer alarm)
{
    public TimerRunState? Current { get; private set; }

    /// <summary>Bouton qui a démarré le minuteur courant — permet à Home.razor de savoir SUR QUELLE touche afficher
    /// le compte (voir TimerFormat), et à ActionService de basculer démarrer/arrêter au clic sur la même touche.</summary>
    public Guid? OriginButtonId { get; private set; }

    public event Action? Changed;

    public bool IsRunningFor(Guid buttonId) => Current is not null && OriginButtonId == buttonId;

    public void Start(TimerConfig timerConfig, string label, Workspace workspace, DeckButton originButton)
    {
        Current?.Dispose();
        alarm.Stop();

        var state = new TimerRunState(timerConfig, label);
        state.Changed += () => Changed?.Invoke();
        state.PhaseEnded += phase => OnPhaseEnded(phase, workspace, originButton);
        state.Finished += () => Changed?.Invoke();

        Current = state;
        OriginButtonId = originButton.Id;
        Changed?.Invoke();
    }

    private void OnPhaseEnded(TimerPhase phase, Workspace workspace, DeckButton originButton)
    {
        switch (phase.SoundKind)
        {
            case TimerSoundKind.Alarm:
                alarm.PlayAlarm();
                break;

            case TimerSoundKind.Script:
                if (!string.IsNullOrWhiteSpace(phase.SoundScript))
                {
                    var pc = config.Current.PcFor(workspace, originButton);
                    _ = runner.RunScriptAsync(pc, phase.SoundScript, phase.Label);
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
        OriginButtonId = null;
        Changed?.Invoke();
    }
}
