using System.Text.Json.Serialization;

namespace StreamDeck.ScriptBuilder.Models;

/// <summary>Comment une <see cref="ScheduleRule"/> décide qu'elle doit se déclencher.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ScheduleMode
{
    /// <summary>Toutes les X minutes (avec, en option, une plage horaire active dans la journée).</summary>
    Interval,

    /// <summary>À des heures précises de la journée (ex : 08:00, 12:30).</summary>
    DailyTimes
}

/// <summary>
/// Un déclenchement automatique d'un script, géré côté serveur (indépendamment de l'application, qui n'a besoin
/// d'être ouverte que pour créer ou modifier la règle). Enregistrée dans Schedules.json, à côté de server.ps1 —
/// jamais dans le script lui-même, pour qu'un même script puisse avoir plusieurs planifications, et pour que le
/// serveur puisse mettre à jour <see cref="LastFiredUtc"/> sans avoir à réécrire (ni même comprendre) le .ps1.
/// </summary>
public class ScheduleRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];

    /// <summary>Nom du script (comme appelé sur le Stream Deck, sans le dossier) — voir <see cref="ScriptProject.Route"/>.</summary>
    public string ScriptName { get; set; } = "";

    public bool Enabled { get; set; } = true;

    public ScheduleMode Mode { get; set; } = ScheduleMode.Interval;

    // ----- Mode Interval -----
    public int IntervalMinutes { get; set; } = 30;

    /// <summary>Plage active dans la journée (HH:mm) : les deux vides = toute la journée.</summary>
    public string ActiveFrom { get; set; } = "";
    public string ActiveTo { get; set; } = "";

    // ----- Mode DailyTimes -----
    /// <summary>Heures de déclenchement (HH:mm), une ou plusieurs.</summary>
    public List<string> Times { get; set; } = new();

    /// <summary>Nombre de fois où le script est relancé à chaque déclenchement (1 = une seule fois, pas de répétition).</summary>
    public int Repeat { get; set; } = 1;

    /// <summary>Attente entre deux lancements d'une même série (ignorée si <see cref="Repeat"/> vaut 1).</summary>
    public int RepeatDelaySeconds { get; set; } = 5;

    /// <summary>Dernier déclenchement (UTC) : géré uniquement par le serveur, jamais par l'application.</summary>
    public DateTime? LastFiredUtc { get; set; }
}

/// <summary>Contenu de Schedules.json : toutes les règles de planification, tous scripts confondus.</summary>
public class ScheduleFile
{
    public List<ScheduleRule> Schedules { get; set; } = new();
}
