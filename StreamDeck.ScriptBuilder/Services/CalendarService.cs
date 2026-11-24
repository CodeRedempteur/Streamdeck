using StreamDeck.Models;
using StreamDeck.ScriptBuilder.Models;
using StreamDeck.Services;

namespace StreamDeck.ScriptBuilder.Services;

/// <summary>
/// Vérifie les créneaux/réveils de l'onglet « Calendrier » (v1.37, voir <see cref="CalendarEntry"/>) et déclenche leur rappel (alarme ou
/// script, voir <see cref="TimerSoundKind"/>) au bon moment.
/// </summary>
/// <remarks>
/// v1.37.4 : possède maintenant son propre minuteur (<see cref="_timer"/>), démarré une seule fois dans le
/// constructeur de ce service SINGLETON — demandé explicitement ("rappel sur toute l'app") : avant, la vérification
/// périodique vivait dans Widget.razor (<c>_calendarTimer</c>, <c>OnInitializedAsync</c>/<c>Dispose</c>) et
/// s'arrêtait dès que le widget flottant se fermait, même si l'app restait ouverte sur un autre onglet (Scripts,
/// Calendrier…) — les rappels ne sonnaient donc que si le widget était affiché au bon moment. Maintenant le
/// minuteur tourne tant que l'app tourne, peu importe la fenêtre/l'onglet affiché, à condition que ce service soit
/// bien instancié au lancement (voir <c>MauiProgram.CreateMauiApp</c>, résolution forcée juste après
/// <c>builder.Build()</c> — sinon un service "Singleton" n'est créé qu'à la première utilisation, ici potentiellement
/// jamais si le widget ne s'ouvre pas). Un son "Script" référence un identifiant de la bibliothèque de scripts
/// (<see cref="ScriptStore"/>) et le lance via <see cref="ScriptTester"/> — exactement le même mécanisme que le son
/// "Script" d'un minuteur (voir TimerPhase/WidgetTimerService) : c'est ainsi que le calendrier reste "relié" au
/// gestionnaire de scripts, comme demandé à l'origine.
/// </remarks>
public sealed class CalendarService : IDisposable
{
    private readonly BuilderConfigService config;
    private readonly ScriptStore store;
    private readonly ScriptTester tester;
    private readonly IAlarmPlayer alarm;
    private readonly Timer timer;

    /// <summary>Fenêtre de tolérance (minutes) dans laquelle un rappel est considéré "dû" — plus large que l'intervalle
    /// d'appel de <see cref="Check"/> pour ne rien manquer si l'app était fermée pile au bon moment.</summary>
    private const int FireWindowMinutes = 2;

    public CalendarService(BuilderConfigService config, ScriptStore store, ScriptTester tester, IAlarmPlayer alarm)
    {
        this.config = config;
        this.store = store;
        this.tester = tester;
        this.alarm = alarm;
        // Départ à 0 (comme les minuteurs comparables de Widget.razor) : rattrape tout de suite un rappel arrivé à
        // échéance pendant que l'app était complètement fermée, plutôt que d'attendre 30s. Même rythme qu'avant.
        timer = new Timer(_ => Check(), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    public void Check()
    {
        try
        {
            var now = DateTime.Now;
            CheckEntries(now);
            CheckDayAlarms(now);
        }
        catch
        {
            // Un minuteur de fond qui plante une fois ne doit jamais arrêter de vérifier les fois suivantes — mieux
            // vaut rater un rappel isolé que perdre tous les suivants silencieusement.
        }
    }

    private void Fire(TimerSoundKind kind, string scriptId)
    {
        switch (kind)
        {
            case TimerSoundKind.Alarm:
                alarm.PlayAlarm();
                break;

            case TimerSoundKind.Script:
                if (!string.IsNullOrWhiteSpace(scriptId))
                {
                    var info = store.List().FirstOrDefault(s => s.Id.Equals(scriptId, StringComparison.OrdinalIgnoreCase));
                    if (info is not null) _ = tester.RunAsync(info.Path);
                }
                break;
        }
    }

    /// <summary>Un créneau s'applique au jour <paramref name="today"/> soit parce qu'il est récurrent et tombe sur
    /// le même jour de semaine, soit parce qu'il est ponctuel et sa date correspond exactement.</summary>
    private static bool AppliesTo(CalendarEntry entry, DateOnly today, DayOfWeek dayOfWeek) =>
        entry.Recurring ? entry.Day == dayOfWeek : entry.Date == today;

    private void CheckEntries(DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        var startDue = config.Current.CalendarEntries
            .Where(e => e.NotifyAtStart && AppliesTo(e, today, now.DayOfWeek) && e.LastFiredStartDate != today
                        && now.TimeOfDay >= e.Start && now.TimeOfDay < e.Start.Add(TimeSpan.FromMinutes(FireWindowMinutes)))
            .ToList();
        var endDue = config.Current.CalendarEntries
            .Where(e => e.NotifyAtEnd && AppliesTo(e, today, now.DayOfWeek) && e.LastFiredEndDate != today
                        && now.TimeOfDay >= e.End && now.TimeOfDay < e.End.Add(TimeSpan.FromMinutes(FireWindowMinutes)))
            .ToList();
        if (startDue.Count == 0 && endDue.Count == 0) return;

        foreach (var e in startDue) Fire(e.StartSoundKind, e.StartSoundScript);
        foreach (var e in endDue) Fire(e.EndSoundKind, e.EndSoundScript);

        var startIds = startDue.Select(e => e.Id).ToHashSet();
        var endIds = endDue.Select(e => e.Id).ToHashSet();
        config.Update(c =>
        {
            foreach (var e in c.CalendarEntries)
            {
                if (startIds.Contains(e.Id)) e.LastFiredStartDate = today;
                if (endIds.Contains(e.Id)) e.LastFiredEndDate = today;
            }
        });
    }

    private void CheckDayAlarms(DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        var due = config.Current.CalendarDayNotes
            .Where(d => d.AlarmEnabled && !d.AlarmFired && d.Date == today
                        && now.TimeOfDay >= d.AlarmTime && now.TimeOfDay < d.AlarmTime.Add(TimeSpan.FromMinutes(FireWindowMinutes)))
            .ToList();
        if (due.Count == 0) return;

        foreach (var _ in due) alarm.PlayAlarm();

        var dueIds = due.Select(d => d.Id).ToHashSet();
        config.Update(c =>
        {
            foreach (var d in c.CalendarDayNotes.Where(d => dueIds.Contains(d.Id)))
                d.AlarmFired = true;
        });
    }

    public void Dispose() => timer.Dispose();
}
