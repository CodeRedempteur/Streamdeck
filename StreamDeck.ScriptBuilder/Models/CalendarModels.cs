using StreamDeck.Models;

namespace StreamDeck.ScriptBuilder.Models;

/// <summary>
/// Un créneau du calendrier (v1.37.2, remplace <see cref="LegacyCalendarRoutine"/> ET <see cref="LegacyCalendarTask"/>
/// — demandé explicitement : "routine de la semaine et tache frequente ne doivent pas etre des zone mais plutot une
/// option lorsque qu'on click sur un jour du calendrier avec en option option récurrente... dans le calendrier on
/// dois pouvoir ajouté plus que une tache par jour et coupé par tranche de temps"). Un seul modèle, créé depuis la
/// modale d'un jour (Calendar.razor) : toujours un créneau avec une heure de début/fin (ex. 8h–11h « Sport »), soit
/// pour CE jour précis (<see cref="Recurring"/> = faux, <see cref="Date"/>), soit répété chaque semaine sur le même
/// jour (<see cref="Recurring"/> = vrai, <see cref="Day"/>) — ce que l'ancienne « routine » faisait déjà. Peut
/// sonner/lancer un script au DÉBUT et/ou à la FIN du créneau (voir CalendarService), exactement comme avant.
/// </summary>
public class CalendarEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Label { get; set; } = "";

    /// <summary>Détail facultatif du créneau (v1.37.3, demandé explicitement : "un bouton I qui permet de voir la
    /// description comme pour les todo") — affiché dans le widget via le bouton info à côté du créneau, pas dans
    /// l'aperçu compact (qui reste court) ; même esprit que <see cref="TodoItem.Description"/>.</summary>
    public string Description { get; set; } = "";

    /// <summary>Couleur du créneau (hex), affichée dans la grille du mois et dans la modale du jour.</summary>
    public string Color { get; set; } = "#3FA7FF";

    /// <summary>Faux (par défaut) : ce créneau n'existe que le <see cref="Date"/> précis. Vrai : il se répète chaque
    /// semaine sur <see cref="Day"/>, quelle que soit l'année affichée dans le calendrier.</summary>
    public bool Recurring { get; set; }

    /// <summary>Utilisé seulement si <see cref="Recurring"/> est vrai.</summary>
    public DayOfWeek Day { get; set; } = DayOfWeek.Monday;

    /// <summary>Utilisé seulement si <see cref="Recurring"/> est faux.</summary>
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public TimeSpan Start { get; set; } = new(9, 0, 0);
    public TimeSpan End { get; set; } = new(10, 0, 0);

    public bool NotifyAtStart { get; set; }
    public TimerSoundKind StartSoundKind { get; set; } = TimerSoundKind.Alarm;
    public string StartSoundScript { get; set; } = "";

    public bool NotifyAtEnd { get; set; }
    public TimerSoundKind EndSoundKind { get; set; } = TimerSoundKind.Alarm;
    public string EndSoundScript { get; set; } = "";

    /// <summary>Date (calendaire, pas la semaine) pour laquelle le rappel de DÉBUT/FIN a déjà sonné — évite de
    /// ressonner en boucle le même jour pendant que CalendarService tourne ; fonctionne pareil pour un créneau
    /// récurrent (comparé à la date du jour) ou ponctuel (ne se redéclenchera jamais, son jour ne revient pas).</summary>
    public DateOnly? LastFiredStartDate { get; set; }
    public DateOnly? LastFiredEndDate { get; set; }

    public CalendarEntry Clone() => new()
    {
        Id = Id, Label = Label, Description = Description, Color = Color, Recurring = Recurring, Day = Day, Date = Date, Start = Start, End = End,
        NotifyAtStart = NotifyAtStart, StartSoundKind = StartSoundKind, StartSoundScript = StartSoundScript,
        NotifyAtEnd = NotifyAtEnd, EndSoundKind = EndSoundKind, EndSoundScript = EndSoundScript,
        LastFiredStartDate = LastFiredStartDate, LastFiredEndDate = LastFiredEndDate
    };
}

/// <summary>Fréquence d'une <see cref="LegacyCalendarTask"/> — OBSOLÈTE, migration seulement (voir ci-dessous).</summary>
public enum CalendarTaskFrequency
{
    Once = 0,
    Daily = 1,
    Weekly = 2,
    Monthly = 3
}

/// <summary>
/// OBSOLÈTE depuis v1.37.2 (remplacé par <see cref="CalendarEntry"/>, voir sa doc) : gardé uniquement pour
/// désérialiser une config écrite par la v1.37.0/1.37.1 — <c>BuilderConfigService.MigrateLegacyCalendarRoutinesAndTasks</c>
/// convertit chaque entrée en <see cref="CalendarEntry"/> équivalente au premier chargement, puis vide la liste.
/// </summary>
public class LegacyCalendarTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public DateTime At { get; set; } = DateTime.Today.AddHours(9);
    public CalendarTaskFrequency Frequency { get; set; } = CalendarTaskFrequency.Once;
    public TimerSoundKind SoundKind { get; set; } = TimerSoundKind.Alarm;
    public string SoundScript { get; set; } = "";
    public bool Done { get; set; }
}

/// <summary>
/// OBSOLÈTE depuis v1.37.2 (remplacé par <see cref="CalendarEntry"/>, voir sa doc) : gardé uniquement pour
/// désérialiser une config écrite par la v1.37.0/1.37.1 — <c>BuilderConfigService.MigrateLegacyCalendarRoutinesAndTasks</c>
/// convertit chaque entrée en <see cref="CalendarEntry"/> équivalente (<see cref="CalendarEntry.Recurring"/> = vrai)
/// au premier chargement, puis vide la liste.
/// </summary>
public class LegacyCalendarRoutine
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Label { get; set; } = "Routine";
    public string Color { get; set; } = "#3FA7FF";
    public DayOfWeek Day { get; set; } = DayOfWeek.Monday;
    public TimeSpan Start { get; set; } = new(9, 0, 0);
    public TimeSpan End { get; set; } = new(10, 0, 0);
    public bool NotifyAtStart { get; set; }
    public TimerSoundKind StartSoundKind { get; set; } = TimerSoundKind.Alarm;
    public string StartSoundScript { get; set; } = "";
    public bool NotifyAtEnd { get; set; }
    public TimerSoundKind EndSoundKind { get; set; } = TimerSoundKind.Alarm;
    public string EndSoundScript { get; set; } = "";
    public DateOnly? LastFiredStartDate { get; set; }
    public DateOnly? LastFiredEndDate { get; set; }
}

/// <summary>
/// Notes/pièces jointes/réveil attachés à un JOUR précis du calendrier (v1.37, demandé explicitement : "pouvoir
/// mettre des pièce jointe dans les jour du calendrier et des rappel réveil mettable sur certain [jours]"). Une
/// entrée n'existe que pour les jours qui en ont réellement besoin (créée à la demande depuis Calendar.razor).
/// </summary>
public class CalendarDayNote
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateOnly Date { get; set; }
    public string Note { get; set; } = "";

    /// <summary>Fichiers attachés : juste leur chemin sur le disque (même principe que l'image d'une touche
    /// épinglée, voir PinImageResolver) — pas de copie encodée dans les réglages.</summary>
    public List<string> AttachmentPaths { get; set; } = new();

    public bool AlarmEnabled { get; set; }
    public TimeSpan AlarmTime { get; set; } = new(7, 0, 0);

    /// <summary>Le réveil de ce jour a déjà sonné — un jour précis ne revient jamais, inutile de le remettre à zéro.</summary>
    public bool AlarmFired { get; set; }

    public CalendarDayNote Clone() => new()
    {
        Id = Id, Date = Date, Note = Note, AttachmentPaths = new List<string>(AttachmentPaths),
        AlarmEnabled = AlarmEnabled, AlarmTime = AlarmTime, AlarmFired = AlarmFired
    };
}
