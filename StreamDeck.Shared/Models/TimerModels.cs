namespace StreamDeck.Models;

/// <summary>Décompte (comme une minuterie de cuisson) ou chronomètre (compte le temps écoulé, utile pour un
/// Pomodoro ou une séance de sport rythmée).</summary>
public enum TimerMode
{
    Countdown = 0,
    Chrono = 1
}

/// <summary>Ce qui se passe à la fin d'une phase (ou de chaque intervalle en mode Chrono).</summary>
public enum TimerSoundKind
{
    /// <summary>Rien (phase silencieuse — utile pour une phase de travail dans un Pomodoro, par exemple).</summary>
    None = 0,

    /// <summary>Alarme par défaut de l'appareil/appli (voir IAlarmPlayer), en boucle jusqu'à l'arrêt manuel.</summary>
    Alarm = 1,

    /// <summary>Lance un script — ex. un script qui ouvre Spotify — exactement comme un bouton de type Script.</summary>
    Script = 2
}

/// <summary>
/// Une phase d'un timer : en mode Décompte, une seule phase normalement (ou plusieurs si on veut par ex. enchaîner
/// plusieurs décomptes) ; en mode Chrono/Pomodoro, plusieurs phases qui s'enchaînent ("Travail" 15 min, "Pause" 5
/// min...), avec <see cref="TimerConfig.Repeat"/> pour les répéter en boucle.
/// </summary>
public class TimerPhase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Label { get; set; } = "Minuteur";
    public int DurationSeconds { get; set; } = 60;
    public TimerSoundKind SoundKind { get; set; } = TimerSoundKind.Alarm;

    /// <summary>SoundKind.Script seulement : nom du script à lancer (ex. un script qui ouvre Spotify).</summary>
    public string SoundScript { get; set; } = "";

    public TimerPhase Clone() => new()
    {
        Label = Label,
        DurationSeconds = DurationSeconds,
        SoundKind = SoundKind,
        SoundScript = SoundScript
    };
}

/// <summary>
/// Réglages d'un timer — utilisé à la fois par <see cref="DeckButton.Timer"/>/<see cref="DeckButton.AttachedTimer"/>
/// (app téléphone/Raspberry) et par <c>WidgetPin</c> côté StreamDeck.ScriptBuilder (widget de bureau) : un seul
/// modèle, un seul composant d'édition partagé (<c>TimerConfigEditor</c>), comme demandé ("même interphase...").
/// </summary>
public class TimerConfig
{
    public TimerMode Mode { get; set; } = TimerMode.Countdown;

    public List<TimerPhase> Phases { get; set; } = new() { new TimerPhase() };

    /// <summary>Recommence au début une fois la dernière phase terminée (ex. Pomodoro : Travail/Pause en boucle).</summary>
    public bool Repeat { get; set; }

    /// <summary>Mode Décompte, une seule phase seulement : la durée n'est pas réglée ici mais demandée au moment du
    /// clic (voir TimerOverlay, état "en attente") — ex. "gâteau" où la durée change selon la recette.</summary>
    public bool PromptDuration { get; set; }

    // ----- Affichage (demandé : "pouvoir paramétré... si on veux voir les seconde minute heure") -----
    public bool ShowHours { get; set; } = true;
    public bool ShowMinutes { get; set; } = true;
    public bool ShowSeconds { get; set; } = true;

    public TimerConfig Clone() => new()
    {
        Mode = Mode,
        Phases = Phases.Select(p => p.Clone()).ToList(),
        Repeat = Repeat,
        PromptDuration = PromptDuration,
        ShowHours = ShowHours,
        ShowMinutes = ShowMinutes,
        ShowSeconds = ShowSeconds
    };
}

/// <summary>
/// Minuteur/chronomètre en cours d'exécution — moteur autonome (pas d'injection de dépendances), utilisé aussi bien
/// par <c>TimerService</c> (app téléphone/Raspberry) que par l'équivalent léger côté widget : la logique de
/// décompte/avancement de phase est écrite une seule fois ici, chaque hôte décide seulement de ce qui se passe à la
/// fin d'une phase (son <see cref="PhaseEnded"/>) via <see cref="TimerSoundKind"/>.
/// </summary>
public class TimerRunState : IDisposable
{
    public TimerConfig Config { get; }
    public string Label { get; }

    /// <summary>Mode Décompte + une seule phase + PromptDuration : en attente de la durée (voir ConfirmPending),
    /// rien ne s'écoule encore.</summary>
    public bool Pending { get; private set; }

    public bool Paused { get; private set; }
    public int PhaseIndex { get; private set; }

    /// <summary>Secondes restantes (Décompte) ou écoulées (Chrono) dans la phase courante — c'est toujours la
    /// valeur à afficher.</summary>
    public int DisplaySeconds { get; private set; }

    public TimerPhase CurrentPhase => Config.Phases[Math.Clamp(PhaseIndex, 0, Config.Phases.Count - 1)];

    /// <summary>Déclenché à chaque tic ou changement d'état — l'UI se contente de se réabonner et d'appeler StateHasChanged.</summary>
    public event Action? Changed;

    /// <summary>Une phase vient de se terminer : à l'hôte de jouer le son/l'action réglée (<see cref="TimerPhase.SoundKind"/>).</summary>
    public event Action<TimerPhase>? PhaseEnded;

    /// <summary>Toutes les phases sont terminées et il n'y a pas de répétition : le timer s'arrête de lui-même.</summary>
    public event Action? Finished;

    private System.Threading.Timer? _ticker;

    public TimerRunState(TimerConfig config, string label)
    {
        Config = config.Clone();
        if (Config.Phases.Count == 0) Config.Phases.Add(new TimerPhase());
        Label = label;

        Pending = Config.PromptDuration && Config.Mode == TimerMode.Countdown && Config.Phases.Count == 1;
        if (!Pending) BeginPhase(0);
    }

    /// <summary>Confirme la durée demandée au clic (secondes) et démarre réellement le décompte.</summary>
    public void ConfirmPending(int seconds)
    {
        if (!Pending) return;
        Pending = false;
        Config.Phases[0].DurationSeconds = Math.Max(1, seconds);
        BeginPhase(0);
    }

    public void TogglePause()
    {
        if (Pending) return;
        Paused = !Paused;
        Changed?.Invoke();
    }

    /// <summary>Passe manuellement à la phase suivante (bouton "passer").</summary>
    public void SkipPhase()
    {
        if (Pending) return;
        Advance();
    }

    private void BeginPhase(int index)
    {
        PhaseIndex = index;
        DisplaySeconds = Config.Mode == TimerMode.Countdown ? Math.Max(0, Config.Phases[index].DurationSeconds) : 0;
        Paused = false;
        _ticker?.Dispose();
        _ticker = new System.Threading.Timer(_ => Tick(), null, 1000, 1000);
        Changed?.Invoke();
    }

    private void Tick()
    {
        if (Paused || Pending) return;

        var phase = Config.Phases[PhaseIndex];
        if (Config.Mode == TimerMode.Countdown)
        {
            DisplaySeconds = Math.Max(0, DisplaySeconds - 1);
            if (DisplaySeconds <= 0) { EndPhase(phase); return; }
        }
        else
        {
            DisplaySeconds++;
            if (DisplaySeconds >= Math.Max(1, phase.DurationSeconds)) { EndPhase(phase); return; }
        }

        Changed?.Invoke();
    }

    private void EndPhase(TimerPhase phase)
    {
        PhaseEnded?.Invoke(phase);
        Advance();
    }

    private void Advance()
    {
        var next = PhaseIndex + 1;
        if (next >= Config.Phases.Count)
        {
            if (Config.Repeat) next = 0;
            else
            {
                _ticker?.Dispose();
                _ticker = null;
                Finished?.Invoke();
                return;
            }
        }
        BeginPhase(next);
    }

    public void Dispose() => _ticker?.Dispose();
}

/// <summary>
/// v1.36.1, "le timer remplace le nom du bouton sur le bouton" : texte court affiché directement SUR la touche
/// (plus d'affichage plein écran pour un décompte/chrono en cours — seule la demande de durée, Pending, garde un
/// popup). Centralisé ici pour que Home.razor (téléphone), TimerOverlay (popup Pending) et Widget.razor (touches du
/// widget) affichent exactement le même texte.
/// </summary>
public static class TimerFormat
{
    public static string Time(TimerRunState state)
    {
        var total = Math.Max(0, state.DisplaySeconds);
        var h = total / 3600;
        var m = total / 60 % 60;
        var s = total % 60;

        var cfg = state.Config;
        var showHours = cfg.ShowHours && h > 0;
        var showMinutes = cfg.ShowMinutes || showHours;

        if (showHours) return $"{h}:{m:D2}:{s:D2}";
        if (showMinutes) return $"{m}:{s:D2}";
        return $"{s}";
    }

    /// <summary>Texte complet pour la touche : l'heure, précédée du nom de la phase courante quand il y en a
    /// plusieurs (ex. Pomodoro "Pause 4:12") — tient sur 2 lignes (voir .keytile-label, déjà limité à 2 lignes).</summary>
    public static string Label(TimerRunState state) =>
        state.Config.Phases.Count > 1 ? $"{state.CurrentPhase.Label} {Time(state)}" : Time(state);
}
