using System.Net.Sockets;
using System.Text.Json;
using StreamDeck.Models;

namespace StreamDeck.Services;

/// <param name="Ok">La requête est partie (ou a abouti).</param>
/// <param name="Message">Message affiché à l'utilisateur.</param>
/// <param name="Warning">La requête est partie mais le résultat est inconnu (pas de réponse du serveur).</param>
public record RunResult(bool Ok, string Message, bool Warning = false);

/// <summary>Volume du PC. <paramref name="Estimated"/> : niveau approximatif (le serveur pilote alors les touches volume du clavier).</summary>
public record AudioState(int Level, bool Muted, bool Estimated = false, string? Warning = null);

/// <summary>Parle au serveur d'un PC (server.ps1). Aucune méthode ne lève d'exception : tout devient un résultat.</summary>
public class ScriptRunner(RequestPauseService pause)
{
    // Au-delà, on considère que la requête est partie et que le script tourne (on n'attend pas plus longtemps).
    private static readonly TimeSpan ResponseWait = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MaxScriptWait = TimeSpan.FromMinutes(3);

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    // Réponse brute du serveur (ou raison de l'absence de réponse).
    // Paused : la requête n'est même pas partie, l'envoi au serveur est en pause (RequestPauseService).
    private record Reply(int? Status, string Body, bool TimedOut, Exception? Error, bool Paused = false)
    {
        public bool Success => Status is >= 200 and < 300;
        public bool GotAnswer => Status is not null;
    }

    private const string PausedMessage = "Envoi au serveur en pause — réactive-le dans la barre du haut.";

    // ---------- Scripts ----------

    /// <summary>Lance un script du dossier Script du serveur.</summary>
    /// <param name="waitForEnd">Attendre la fin du script (suivi par le serveur, sans bloquer les autres requêtes).</param>
    public async Task<RunResult> RunScriptAsync(PcProfile pc, string script, string label, bool waitForEnd = false,
        IReadOnlyDictionary<string, string>? variables = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(script))
            return new RunResult(false, "Aucun script défini pour ce bouton");

        if (!Uri.TryCreate(pc.ServerUrl, UriKind.Absolute, out _))
            return new RunResult(false, "URL du serveur invalide (voir Réglages)");

        // Chaque segment du chemin est encodé (un nom de script peut contenir un espace).
        var path = string.Join('/', script.Trim().Trim('/').Split('/').Select(Uri.EscapeDataString));

        // Variables Stream Deck Script Builder ({season}, {episode}...) : transmises en paramètres de requête,
        // le serveur les passe au script sous ces mêmes noms (voir Get-NfcScriptParams côté server.ps1).
        var query = variables is null || variables.Count == 0
            ? null
            : string.Join('&', variables.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        try
        {
            var reply = await SendAsync(pc, path, query, ResponseWait, ct);

            if (reply.Success)
            {
                var jobId = waitForEnd ? ReadString(reply.Body, "job") : null;
                return jobId is null
                    ? new RunResult(true, $"{label} : lancé")
                    : await WaitForJobAsync(pc, jobId, label, ct);
            }

            return await ExplainFailureAsync(pc, reply, label, script, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new RunResult(false, "Annulé");
        }
    }

    // ---------- Actions Windows (/_/…) ----------

    public async Task<RunResult> RunCommandAsync(PcProfile pc, PcCommand command, string label, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(pc.ServerUrl, UriKind.Absolute, out _))
            return new RunResult(false, "URL du serveur invalide (voir Réglages)");

        try
        {
            var reply = await SendAsync(pc, command.Path, command.Query, ResponseWait, ct);

            if (reply.Success)
                return new RunResult(true, Describe(command, reply.Body, label));

            if (reply.Status == 404)
                return new RunResult(false, "Action inconnue du serveur : installe la dernière version de server.ps1");

            return await ExplainFailureAsync(pc, reply, label, command.Id, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new RunResult(false, "Annulé");
        }
    }

    private static string Describe(PcCommand command, string body, string label)
    {
        if (command.Path == "_/audio" && TryReadAudio(body) is { } audio)
            return audio.Muted ? "Son coupé" : audio.Estimated ? $"Volume ~{audio.Level} %" : $"Volume {audio.Level} %";

        return $"{label} : envoyé";
    }

    // ---------- Affichage plein écran sur le PC (/_/info) ----------

    /// <summary>Demande au PC d'ouvrir une fenêtre plein écran (heure, date, jour ou image). Le PC répond dès que la
    /// fenêtre s'ouvre, sans attendre qu'elle se ferme (fermeture par la croix, Échap, ou la durée réglée).</summary>
    public async Task<RunResult> ShowInfoAsync(PcProfile pc, InfoKind kind, string? imageSource, int durationSeconds, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(pc.ServerUrl, UriKind.Absolute, out _))
            return new RunResult(false, "URL du serveur invalide (voir Réglages)");

        var parts = new List<string> { $"kind={InfoKindQuery(kind)}", $"duration={Math.Max(0, durationSeconds)}" };
        if (!string.IsNullOrWhiteSpace(imageSource)) parts.Add($"image={Uri.EscapeDataString(imageSource)}");
        var query = string.Join('&', parts);

        try
        {
            var reply = await SendAsync(pc, "_/info", query, ResponseWait, ct);

            if (reply.Success) return new RunResult(true, "Affiché sur le PC");
            if (reply.Status == 404) return new RunResult(false, "Affichage plein écran inconnu du serveur : installe la dernière version de server.ps1");

            return await ExplainFailureAsync(pc, reply, "Affichage PC", "info", ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new RunResult(false, "Annulé");
        }
    }

    private static string InfoKindQuery(InfoKind kind) => kind switch
    {
        InfoKind.Image => "image",
        InfoKind.Date => "date",
        InfoKind.DayOfWeek => "dayofweek",
        _ => "clock"
    };

    // ---------- Volume ----------

    public Task<AudioState?> GetAudioAsync(PcProfile pc, CancellationToken ct = default) =>
        AudioAsync(pc, null, ct);

    /// <summary>Règle le volume (0-100), le modifie de <paramref name="delta"/>, ou change le mode muet (on / off / toggle).</summary>
    public Task<AudioState?> SetAudioAsync(PcProfile pc, int? level = null, int? delta = null, string? mute = null, CancellationToken ct = default)
    {
        var parts = new List<string>();
        if (level is { } l) parts.Add($"level={Math.Clamp(l, 0, 100)}");
        if (delta is { } d) parts.Add($"delta={Math.Clamp(d, -100, 100)}");
        if (mute is { } m) parts.Add($"mute={Uri.EscapeDataString(m)}");

        return AudioAsync(pc, parts.Count == 0 ? null : string.Join('&', parts), ct);
    }

    /// <summary>Raison du dernier échec d'une opération de volume (pour l'afficher à l'utilisateur).</summary>
    public string? LastAudioError { get; private set; }

    private async Task<AudioState?> AudioAsync(PcProfile pc, string? query, CancellationToken ct)
    {
        try
        {
            var reply = await SendAsync(pc, "_/audio", query, TimeSpan.FromSeconds(4), ct);
            if (reply.Success)
            {
                LastAudioError = null;
                return TryReadAudio(reply.Body);
            }

            LastAudioError = reply.Paused
                ? PausedMessage
                : reply.GotAnswer
                    ? ServerError(reply.Status!.Value, reply.Body)
                    : "le PC ne répond pas";
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private static AudioState? TryReadAudio(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("level", out var level) || !level.TryGetInt32(out var value)) return null;

            var muted = root.TryGetProperty("muted", out var m) && m.ValueKind == JsonValueKind.True;
            var estimated = root.TryGetProperty("estimated", out var e) && e.ValueKind == JsonValueKind.True;
            var warning = root.TryGetProperty("warning", out var w) && w.ValueKind == JsonValueKind.String ? w.GetString() : null;
            return new AudioState(Math.Clamp(value, 0, 100), muted, estimated, warning);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // ---------- Test et surveillance ----------

    /// <summary>Test complet pour les réglages : port ouvert, puis token accepté (/_/ping).</summary>
    public async Task<RunResult> TestAsync(PcProfile pc, CancellationToken ct = default)
    {
        var ping = await PingAsync(pc.ServerUrl, ct);
        if (!ping.Ok) return ping;

        try
        {
            var reply = await SendAsync(pc, "_/ping", null, TimeSpan.FromSeconds(5), ct);

            if (reply.Paused) return new RunResult(false, PausedMessage);

            if (reply.Success)
            {
                var name = ReadString(reply.Body, "name");
                var text = name is null ? "Serveur joignable, token accepté" : $"Serveur joignable, token accepté ({name})";

                // Le volume : le serveur dit s'il a dû se rabattre sur les touches du clavier, et pourquoi.
                var audio = await AudioAsync(pc, null, ct);
                if (audio is { Estimated: true })
                    return new RunResult(true, $"{text}. Volume en mode clavier (niveau estimé) : {audio.Warning ?? "raison inconnue"}", Warning: true);
                if (audio is null && LastAudioError is not null)
                    return new RunResult(true, $"{text}. Volume indisponible : {LastAudioError}", Warning: true);

                return new RunResult(true, text);
            }

            return reply.Status switch
            {
                401 or 403 => new RunResult(false, "Serveur joignable, mais token refusé"),
                404 => new RunResult(true, "Serveur joignable. Installe le nouveau server.ps1 pour les actions Windows.", Warning: true),
                _ => new RunResult(false, "Serveur joignable, mais réponse inattendue")
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new RunResult(false, "Annulé");
        }
    }

    /// <summary>Vérifie que le serveur accepte les connexions (simple ouverture de port, aucune requête HTTP envoyée).</summary>
    public async Task<RunResult> PingAsync(string serverUrl, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri))
            return new RunResult(false, "Adresse invalide");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(uri.Host, uri.Port, timeout.Token);
            return new RunResult(true, "Serveur joignable");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new RunResult(false, "Pas de réponse (délai dépassé)");
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return new RunResult(false, "Serveur injoignable");
        }
    }

    // ---------- Mécanique HTTP ----------

    private async Task<Reply> SendAsync(PcProfile pc, string path, string? query, TimeSpan wait, CancellationToken ct)
    {
        // Bouton "pause" de la barre du haut (Home.razor) : la requête ne part même pas.
        if (pause.Paused)
            return new Reply(null, string.Empty, false, null, Paused: true);

        var url = $"{pc.ServerUrl.TrimEnd('/')}/{path}?token={Uri.EscapeDataString(pc.Token)}";
        if (!string.IsNullOrEmpty(query)) url += "&" + query;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            // Une connexion neuve par appui : évite de réutiliser une connexion que le serveur a déjà fermée
            // (cause classique d'erreur sur le 2e bouton).
            request.Headers.ConnectionClose = true;

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(wait);

            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            return new Reply((int)response.StatusCode, body, false, null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Pas de réponse dans le délai : la requête est partie, le script tourne peut-être encore.
            return new Reply(null, string.Empty, true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // HttpRequestException (PC), IOException / WebException / exceptions Java (Android) :
            // souvent le serveur ferme la connexion sans réponse HTTP propre.
            return new Reply(null, string.Empty, false, ex);
        }
    }

    /// <summary>« Le serveur a répondu 500 : raison » (la raison est le texte renvoyé par le serveur, raccourci).</summary>
    private static string ServerError(int code, string body)
    {
        var detail = (body ?? string.Empty).Trim().Replace("\r", " ").Replace("\n", " ");
        if (detail.StartsWith("Erreur:", StringComparison.OrdinalIgnoreCase)) detail = detail[7..].Trim();
        if (detail.Length > 160) detail = detail[..160] + "…";
        return detail.Length == 0 ? $"Le serveur a répondu {code}" : $"Le serveur a répondu {code} : {detail}";
    }

    /// <summary>Traduit une réponse d'erreur (ou l'absence de réponse) en message clair.</summary>
    private async Task<RunResult> ExplainFailureAsync(PcProfile pc, Reply reply, string label, string what, CancellationToken ct)
    {
        if (reply.Paused) return new RunResult(false, PausedMessage);

        if (reply.GotAnswer)
        {
            return new RunResult(false, reply.Status switch
            {
                401 or 403 => "Token refusé par le serveur",
                404 => $"« {what} » introuvable sur le serveur",
                var code => ServerError(code ?? 0, reply.Body)
            });
        }

        // Aucune réponse : si le serveur répond encore à la connexion, la requête est bien partie.
        var ping = await PingAsync(pc.ServerUrl, ct);
        if (!ping.Ok) return new RunResult(false, "Serveur injoignable");

        var detail = reply.TimedOut
            ? $"{label} : envoyé (script en cours)"
            : $"{label} : envoyé, réponse interrompue ({reply.Error?.GetType().Name})";
        return new RunResult(true, detail, Warning: true);
    }

    /// <summary>Attend la fin d'un script lancé sur le serveur (suivi par identifiant de tâche).</summary>
    private async Task<RunResult> WaitForJobAsync(PcProfile pc, string jobId, string label, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + MaxScriptWait;

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), ct);

            var reply = await SendAsync(pc, "_/job", $"id={Uri.EscapeDataString(jobId)}", TimeSpan.FromSeconds(5), ct);
            if (!reply.Success) continue;

            if (ReadString(reply.Body, "state") == "done")
                return new RunResult(true, $"{label} : terminé");
        }

        return new RunResult(true, $"{label} : toujours en cours", Warning: true);
    }

    private static string? ReadString(string body, string property)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
