using System.Text;
using StreamDeck.Models;

namespace StreamDeck.Services;

/// <summary>
/// Résultat d'une phrase reconnue passée à <see cref="VoiceMatcher"/>. <see cref="Option"/> non nul = la
/// correspondance vient d'un choix précis d'un bouton « choix multiple » (<see cref="Button"/> est alors son
/// bouton propriétaire, à passer tel quel à <c>ActionService.ExecuteOptionAsync</c>) ; nul = le bouton lui-même.
/// </summary>
public record VoiceMatchResult(DeckButton? Button, ChoiceOption? Option, bool WakeWordHeard)
{
    /// <summary>Nom à afficher (l'option si c'en est une, sinon le bouton).</summary>
    public string? Label => Option?.Label ?? Button?.Label;
}

/// <summary>
/// Fait correspondre une phrase reconnue (venant de la reconnaissance vocale d'Android) à un bouton — ou à un choix
/// précis d'un bouton « choix multiple » — à déclencher. Logique pure — aucune dépendance à Android, testable
/// directement. Le service Android natif (écoute réelle du micro) transmet chaque phrase reconnue ici et agit sur
/// ce qui est renvoyé.
///
/// Deux façons pour un bouton — ou un choix d'un bouton « choix multiple » — de se déclencher :
/// - <c>VoiceRequiresWakeWord</c> = faux : reconnu à tout moment, dans n'importe quelle phrase.
/// - = vrai (par défaut) : seulement dans la courte fenêtre qui suit le mot de réveil (une phrase qui contient le
///   mot de réveil ET la commande à la suite compte aussi, dans la même phrase).
/// </summary>
public class VoiceMatcher
{
    private readonly Func<DateTime> _now;
    private DateTime _awakeUntil = DateTime.MinValue;

    public VoiceMatcher(Func<DateTime>? now = null)
    {
        _now = now ?? (() => DateTime.UtcNow);
    }

    /// <summary>Vrai si le mot de réveil a été entendu récemment et qu'une commande est encore attendue.</summary>
    public bool IsAwake => _now() < _awakeUntil;

    /// <summary>Oublie l'éveil en cours (ex : bascule d'écoute désactivée, ou changement d'espace de travail).</summary>
    public void Reset() => _awakeUntil = DateTime.MinValue;

    /// <summary>
    /// Traite une phrase reconnue. <paramref name="buttons"/> doit couvrir tous les boutons candidats (tous les
    /// espaces confondus, ou seulement l'espace actif selon ce que l'appelant choisit d'exposer à la voix) — les
    /// choix d'un bouton « choix multiple » activé sont examinés automatiquement, sans les lister séparément.
    /// </summary>
    public VoiceMatchResult Process(string utterance, VoiceSettings settings, IEnumerable<DeckButton> buttons)
    {
        var normalized = Normalize(utterance);
        if (normalized.Length == 0) return new VoiceMatchResult(null, null, false);

        var candidates = BuildCandidates(buttons).ToList();

        // 1. Déclencheurs directs : reconnus à tout moment, jamais besoin du mot de réveil.
        var direct = candidates.FirstOrDefault(c => !c.RequiresWakeWord && Matches(normalized, c.Triggers));
        if (direct.Button is not null) return new VoiceMatchResult(direct.Button, direct.Option, false);

        // 2. Mot de réveil entendu dans cette phrase : ouvre (ou prolonge) la fenêtre d'écoute d'une commande.
        var wakeWordHeard = false;
        var afterWake = normalized;
        if (!string.IsNullOrWhiteSpace(settings.WakeWord))
        {
            var wake = Normalize(settings.WakeWord);
            var idx = wake.Length > 0 ? normalized.IndexOf(wake, StringComparison.Ordinal) : -1;
            if (idx >= 0)
            {
                wakeWordHeard = true;
                _awakeUntil = _now() + TimeSpan.FromSeconds(Math.Max(1, settings.AwakeSeconds));
                afterWake = normalized[(idx + wake.Length)..].Trim();
            }
        }

        // 3. Éveillé (à l'instant, ou déjà dans la fenêtre) : cherche la commande — dans le reste de CETTE phrase
        //    d'abord (« Cortana lance Spotify » en une seule fois), sinon dans la phrase entière (dite séparément).
        if (wakeWordHeard || IsAwake)
        {
            var toMatch = wakeWordHeard && afterWake.Length > 0 ? afterWake : normalized;
            var awaited = candidates.FirstOrDefault(c => c.RequiresWakeWord && Matches(toMatch, c.Triggers));
            if (awaited.Button is not null)
            {
                _awakeUntil = DateTime.MinValue; // consommé : il faut redire le mot de réveil pour la suivante
                return new VoiceMatchResult(awaited.Button, awaited.Option, wakeWordHeard);
            }
        }

        return new VoiceMatchResult(null, null, wakeWordHeard);
    }

    /// <summary>Tout ce qui peut se déclencher à la voix : chaque bouton avec ses propres phrases, plus — pour un
    /// bouton « choix multiple » — chacun de ses choix qui a les siennes (indépendamment du bouton propriétaire).</summary>
    private static IEnumerable<(DeckButton Button, ChoiceOption? Option, bool RequiresWakeWord, List<string> Triggers)> BuildCandidates(IEnumerable<DeckButton> buttons)
    {
        foreach (var b in buttons.Where(b => b.Enabled))
        {
            if (b.VoiceTriggers.Count > 0)
                yield return (b, null, b.VoiceRequiresWakeWord, b.VoiceTriggers);

            if (b.Kind == ButtonKind.Picker)
                foreach (var o in b.Options.Where(o => o.VoiceTriggers.Count > 0))
                    yield return (b, o, o.VoiceRequiresWakeWord, o.VoiceTriggers);
        }
    }

    private static bool Matches(string normalizedText, List<string> triggers) =>
        triggers.Any(t => !string.IsNullOrWhiteSpace(t) && normalizedText.Contains(Normalize(t), StringComparison.Ordinal));

    /// <summary>Minuscules, accents retirés, espaces superflus réduits — pour que « Café » et « cafe » se valent.
    /// Table explicite plutôt que la normalisation Unicode (FormD) : ne dépend pas d'ICU, qui n'est pas toujours
    /// disponible selon l'environnement (résultat identique partout, y compris en mode « invariant »).</summary>
    public static string Normalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;

        var sb = new StringBuilder(s.Length);
        foreach (var c in s.Trim().ToLowerInvariant())
            sb.Append(FoldAccent(c));

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static char FoldAccent(char c) => c switch
    {
        'à' or 'â' or 'ä' or 'á' or 'ã' or 'å' => 'a',
        'ç' => 'c',
        'é' or 'è' or 'ê' or 'ë' => 'e',
        'î' or 'ï' or 'ì' or 'í' => 'i',
        'ô' or 'ö' or 'ò' or 'ó' or 'õ' => 'o',
        'ù' or 'û' or 'ü' or 'ú' => 'u',
        'ÿ' or 'ý' => 'y',
        'ñ' => 'n',
        _ => c
    };
}
