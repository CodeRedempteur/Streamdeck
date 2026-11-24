namespace StreamDeck.Models;

/// <summary>Une action Windows intégrée au serveur (chemin /_/…).</summary>
public record PcCommand(
    string Id,
    string Label,
    string Description,
    string Icon,
    string Color,
    string Path,
    string Query,
    ConfirmMode DefaultConfirm = ConfirmMode.None);

/// <summary>Catalogue des actions Windows proposées par le serveur (server.ps1 version 2).</summary>
public static class PcCommands
{
    public static readonly IReadOnlyList<PcCommand> All = new List<PcCommand>
    {
        // ----- Média : agit sur le lecteur actif (Spotify, navigateur, VLC…) -----
        new("media.playpause", "Lecture / Pause", "Met la musique ou la vidéo en pause, ou la relance.",
            "fa-solid fa-play", "#10b981", "_/media", "cmd=playpause"),
        new("media.next", "Piste suivante", "Passe au morceau suivant.",
            "fa-solid fa-forward-step", "#0ea5e9", "_/media", "cmd=next"),
        new("media.prev", "Piste précédente", "Revient au morceau précédent.",
            "fa-solid fa-backward-step", "#0ea5e9", "_/media", "cmd=prev"),
        new("media.stop", "Stop", "Arrête la lecture.",
            "fa-solid fa-stop", "#64748b", "_/media", "cmd=stop"),

        // ----- Volume -----
        new("audio.up", "Volume +", "Monte le volume de 5 %.",
            "fa-solid fa-volume-high", "#8b5cf6", "_/audio", "delta=5"),
        new("audio.down", "Volume -", "Baisse le volume de 5 %.",
            "fa-solid fa-volume-low", "#8b5cf6", "_/audio", "delta=-5"),
        new("audio.mute", "Muet", "Coupe ou remet le son.",
            "fa-solid fa-volume-xmark", "#64748b", "_/audio", "mute=toggle"),

        // ----- Écran et session -----
        new("desktop.show", "Afficher le bureau", "Réduit toutes les fenêtres (ou les restaure).",
            "fa-solid fa-desktop", "#14b8a6", "_/desktop", "cmd=show"),
        new("desktop.taskview", "Vue des tâches", "Affiche toutes les fenêtres ouvertes.",
            "fa-solid fa-table-cells-large", "#14b8a6", "_/desktop", "cmd=taskview"),
        new("power.monitoroff", "Éteindre l'écran", "Éteint les écrans, le PC reste allumé.",
            "fa-solid fa-display", "#64748b", "_/power", "cmd=monitoroff"),
        new("power.lock", "Verrouiller", "Verrouille la session Windows.",
            "fa-solid fa-lock", "#f59e0b", "_/power", "cmd=lock"),

        // ----- Alimentation (sensibles : confirmation proposée par défaut) -----
        new("power.sleep", "Mise en veille", "Met le PC en veille (réveil possible par Wake-on-LAN).",
            "fa-solid fa-moon", "#6366f1", "_/power", "cmd=sleep", ConfirmMode.Confirm),
        new("power.hibernate", "Hibernation", "Sauvegarde la session et éteint le PC.",
            "fa-solid fa-bed", "#6366f1", "_/power", "cmd=hibernate", ConfirmMode.Confirm),
        new("power.restart", "Redémarrer", "Redémarre le PC dans 5 secondes.",
            "fa-solid fa-rotate-right", "#f97316", "_/power", "cmd=restart&delay=5", ConfirmMode.Pin),
        new("power.shutdown", "Éteindre le PC", "Éteint le PC dans 5 secondes.",
            "fa-solid fa-power-off", "#ef4444", "_/power", "cmd=shutdown&delay=5", ConfirmMode.Pin),
        new("power.abort", "Annuler l'arrêt", "Annule un arrêt ou un redémarrage en cours.",
            "fa-solid fa-ban", "#64748b", "_/power", "cmd=abort"),
    };

    public static PcCommand? Find(string? id) =>
        All.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
}
