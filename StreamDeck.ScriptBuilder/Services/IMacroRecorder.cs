using StreamDeck.ScriptBuilder.Models;

namespace StreamDeck.ScriptBuilder.Services;

/// <summary>Résultat d'un enregistrement : les évènements capturés, et si l'arrêt vient de la touche prévue (plutôt
/// que de la durée maximale écoulée, ou d'une annulation).</summary>
public record MacroRecordingResult(IReadOnlyList<MacroAction> Actions, bool StoppedByKey);

/// <summary>
/// Enregistre les mouvements de souris, clics et touches au clavier, n'importe où sur le PC (pas seulement dans
/// cette application) — nécessite des « hooks » globaux, propres à Windows. Implémenté par l'hôte Windows ;
/// <see cref="CanRecord"/> vaut faux ailleurs (ou si l'installation des hooks a échoué), pour que l'interface
/// puisse simplement expliquer que ce n'est pas disponible plutôt que d'échouer sans explication.
/// </summary>
public interface IMacroRecorder
{
    bool CanRecord { get; }

    /// <summary>
    /// Enregistre jusqu'à <paramref name="maxDurationSeconds"/>, ou jusqu'à ce que <paramref name="stopKey"/> soit
    /// pressée (elle n'est alors pas incluse dans le résultat, mais continue d'atteindre normalement le programme
    /// qui a le focus). <paramref name="progress"/> reçoit le temps écoulé toutes les ~200 ms, pour l'affichage.
    /// </summary>
    Task<MacroRecordingResult> RecordAsync(int maxDurationSeconds, MacroControlKey stopKey, bool captureMouseMoves,
        IProgress<TimeSpan>? progress, CancellationToken ct = default);
}
