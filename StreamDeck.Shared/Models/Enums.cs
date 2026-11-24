namespace StreamDeck.Models;

/// <summary>Ce que fait un bouton.</summary>
public enum ButtonKind
{
    /// <summary>Lance un script du serveur : http://serveur:8765/NOMDUSCRIPT?token=...</summary>
    Script = 0,

    /// <summary>Réveille un PC éteint ou en veille (Wake-on-LAN).</summary>
    Wake = 1,

    /// <summary>Action Windows intégrée : média, volume, verrouiller, éteindre…</summary>
    PcAction = 2,

    /// <summary>Enchaîne plusieurs étapes : réveil, attente, scripts, pauses…</summary>
    Sequence = 3,

    /// <summary>Curseur de volume du PC.</summary>
    VolumeSlider = 4,

    /// <summary>Précédent / Lecture-Pause / Suivant.</summary>
    MediaControls = 5,

    /// <summary>Ouvre un popup proposant plusieurs choix (scripts, actions…) ; un seul est lancé.</summary>
    Picker = 6,

    /// <summary>Affiche en plein écran une image, l'heure, la date ou le jour — sur l'appli et/ou sur le PC.</summary>
    InfoDisplay = 7,

    /// <summary>Retrouver le téléphone : mode sonnerie + alarme au volume max (Android uniquement, v1.35 ; no-op ailleurs).</summary>
    FindPhone = 8,

    /// <summary>Minuteur/chronomètre configurable (v1.36) — voir DeckButton.Timer/TimerService/TimerOverlay.</summary>
    Timer = 9
}

/// <summary>Ce qu'affiche un bouton "Affichage plein écran".</summary>
public enum InfoKind
{
    Clock = 0,
    Date = 1,
    DayOfWeek = 2,
    Image = 3,

    /// <summary>Météo courante d'une région choisie (v1.34.6, voir WeatherService et DeckButton.InfoWeatherLocationName). Appli uniquement (InfoTarget.Pc non pris en charge).</summary>
    Weather = 4,

    /// <summary>Heure courante d'un fuseau horaire choisi (v1.34.6, voir DeckButton.InfoWorldClockTimeZoneId). Appli uniquement (InfoTarget.Pc non pris en charge).</summary>
    WorldClock = 5
}

/// <summary>Où afficher un bouton "Affichage plein écran".</summary>
public enum InfoTarget
{
    /// <summary>Dans l'appli Stream Deck elle-même (le téléphone/tablette/PC qui a appuyé, ou qui a entendu la voix).</summary>
    App = 0,

    /// <summary>Sur l'écran du PC piloté (fenêtre plein écran ouverte par server.ps1).</summary>
    Pc = 1,

    Both = 2
}

/// <summary>Demande de confirmation avant d'exécuter un bouton.</summary>
public enum ConfirmMode
{
    None = 0,
    Confirm = 1,
    Pin = 2
}

public enum StepType
{
    Script = 0,
    PcAction = 1,
    Wake = 2,
    WaitOnline = 3,
    Delay = 4
}

/// <summary>Espacement entre les tuiles d'une page.</summary>
public enum Spacing
{
    Tight = 0,
    Normal = 1,
    Wide = 2
}

/// <summary>Taille des icônes des tuiles d'une page.</summary>
public enum IconSize
{
    Small = 0,
    Normal = 1,
    Large = 2
}

public static class ButtonKinds
{
    /// <summary>true pour les tuiles qui ne se « pressent » pas (curseur, contrôles média).</summary>
    public static bool IsWidget(ButtonKind kind) => kind is ButtonKind.VolumeSlider or ButtonKind.MediaControls;
}
