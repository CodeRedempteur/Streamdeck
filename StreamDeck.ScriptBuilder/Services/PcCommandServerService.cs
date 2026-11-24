using System.Net;
using System.Net.Sockets;
using StreamDeck.Models;

namespace StreamDeck.ScriptBuilder.Services;

/// <summary>
/// v1.34.17, demandé explicitement ("ajoute le moyen d'exporter depuis le téléphone un config à importer sur le pc
/// donc un bouton envoyer au pc") : petit serveur TCP qui tourne DANS Script Builder lui-même, symétrique du petit
/// serveur de l'app téléphone (StreamDeck.Android/Platforms/Android/PhoneCommandServerService.cs) — reçoit une
/// configuration complète envoyée par le téléphone (voir PcTargetSection côté StreamDeck.Shared) et l'ajoute comme
/// un nouveau modèle dans l'onglet « Téléphone » (<see cref="BuilderConfig.PhoneTemplates"/>, v1.34.16). Complète le
/// sens PC → téléphone (déjà possible depuis la v1.34.16 via PhoneConfigTransferService) par le sens inverse.
///
/// <para>
/// Démarré/arrêté automatiquement selon <see cref="BuilderConfig.PcServer"/> (réglage dans Réglages → « Recevoir
/// depuis le téléphone ») — construit au lancement de l'application via une injection « à vide » dans MainLayout.razor
/// (même principe que <see cref="HotkeyService"/>), pas un vrai hébergeur de services en arrière-plan (cette
/// application n'en a pas).
/// </para>
///
/// <para>
/// Même technique de petit serveur fait main que côté téléphone (voir ce fichier pour le détail) : ligne de requête
/// et en-têtes lus en octets bruts, puis le corps lu en octets exacts selon <c>Content-Length</c> et décodé en
/// UTF-8 — jamais via un <see cref="System.IO.StreamReader"/> ASCII, qui corromprait les accents français d'un
/// corps JSON.
/// </para>
/// </summary>
public class PcCommandServerService
{
    private readonly BuilderConfigService _config;
    private readonly object _lock = new();

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private int _listeningPort = -1;

    public PcCommandServerService(BuilderConfigService config)
    {
        _config = config;
        _config.Changed += Sync;
        Sync();
    }

    private void Sync()
    {
        lock (_lock)
        {
            var settings = _config.Current.PcServer;

            if (!settings.Enabled)
            {
                StopListenerLocked();
                return;
            }

            if (_listener is not null && _listeningPort == settings.Port) return; // déjà actif sur le bon port

            StopListenerLocked();

            _listeningPort = settings.Port;
            var cts = new CancellationTokenSource();
            _cts = cts;

            try
            {
                _listener = new TcpListener(IPAddress.Any, settings.Port);
                _listener.Start();
            }
            catch (Exception)
            {
                // Port déjà utilisé, ou indisponible : rien de mieux à faire que de rester silencieux — comme côté
                // téléphone, le réglage reste "activé" mais n'accepte jamais rien.
                _listener = null;
                return;
            }

            _ = Task.Run(() => ListenLoopAsync(_listener, cts.Token));
        }
    }

    private void StopListenerLocked()
    {
        _cts?.Cancel();
        try { _listener?.Stop(); } catch (Exception) { /* déjà arrêté */ }
        _listener = null;
        _listeningPort = -1;
    }

    private async Task ListenLoopAsync(TcpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(ct);
            }
            catch (Exception)
            {
                break; // écouteur arrêté (réglages changés) ou erreur réseau irrécupérable
            }

            _ = Task.Run(() => HandleClientAsync(client, ct), ct);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using var c = client;
        c.ReceiveTimeout = 10_000;
        c.SendTimeout = 10_000;

        try
        {
            await using System.IO.Stream stream = c.GetStream();

            var (requestLine, headers, leftoverBody) = await ReadRequestHeadAsync(stream, ct);
            if (requestLine is null) return;

            var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var method = parts.Length > 0 ? parts[0] : "GET";
            var path = parts.Length > 1 ? parts[1] : "/";
            var (route, query) = ParsePath(path);

            string? requestBody = null;
            if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase)
                && headers.TryGetValue("content-length", out var contentLengthText)
                && int.TryParse(contentLengthText, out var contentLength) && contentLength > 0)
            {
                requestBody = await ReadBodyAsync(stream, leftoverBody, contentLength, ct);
            }

            var (status, body) = HandleRequest(route, query, requestBody);
            await WriteResponseAsync(stream, status, body, ct);
        }
        catch (Exception)
        {
            // Client mal formé, déconnecté en cours de route, etc. : sans gravité pour un petit serveur local.
        }
    }

    private (string Status, string Body) HandleRequest(string route, IReadOnlyDictionary<string, string> query, string? body)
    {
        var settings = _config.Current.PcServer;
        if (!settings.Enabled) return ("503 Service Unavailable", "Serveur desactive");
        if (string.IsNullOrEmpty(settings.Token) || query.GetValueOrDefault("token") != settings.Token)
            return ("401 Unauthorized", "Jeton invalide");

        if (route == "/ping") return ("200 OK", "OK");
        if (route != "/command") return ("404 Not Found", "Introuvable");

        var action = query.GetValueOrDefault("action", "");
        return action switch
        {
            "importconfig" => ImportConfig(body),
            _ => ("400 Bad Request", "Action inconnue")
        };
    }

    /// <summary>Reçoit la configuration du téléphone et l'ajoute comme nouveau modèle — jamais d'écrasement automatique d'un modèle existant.</summary>
    private (string, string) ImportConfig(string? body)
    {
        var imported = ConfigTransfer.Parse(body, out var error);
        if (imported is null) return ("400 Bad Request", error);

        var template = new PhoneConfigTemplate
        {
            Name = $"Depuis le téléphone ({DateTime.Now:dd/MM HH:mm})",
            Config = imported
        };
        _config.Update(c => c.PhoneTemplates.Add(template));

        var buttons = imported.Workspaces.Sum(w => w.Pages.Sum(p => p.Buttons.Count));
        return ("200 OK", $"Reçu : {imported.Workspaces.Count} espace(s), {buttons} bouton(s) — ajouté comme nouveau modèle dans l'onglet Téléphone.");
    }

    // ----- Lecture bas niveau (identique dans l'esprit à PhoneCommandServerService côté Android) -----

    private static async Task<(string? RequestLine, Dictionary<string, string> Headers, byte[] LeftoverBody)> ReadRequestHeadAsync(
        System.IO.Stream stream, CancellationToken ct)
    {
        var buffer = new List<byte>(512);
        var chunk = new byte[512];

        int HeaderEndIndex()
        {
            for (var i = 0; i + 3 < buffer.Count; i++)
            {
                if (buffer[i] == '\r' && buffer[i + 1] == '\n' && buffer[i + 2] == '\r' && buffer[i + 3] == '\n')
                    return i;
            }
            return -1;
        }

        int headerEnd;
        while ((headerEnd = HeaderEndIndex()) < 0)
        {
            var read = await stream.ReadAsync(chunk, ct);
            if (read == 0) return (null, new Dictionary<string, string>(), Array.Empty<byte>());
            buffer.AddRange(chunk.AsSpan(0, read).ToArray());

            if (buffer.Count > 32 * 1024) return (null, new Dictionary<string, string>(), Array.Empty<byte>()); // en-têtes anormalement longs
        }

        var headerText = System.Text.Encoding.ASCII.GetString(buffer.ToArray(), 0, headerEnd);
        var lines = headerText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            var colon = line.IndexOf(':');
            if (colon < 0) continue;
            headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }

        var leftover = buffer.Skip(headerEnd + 4).ToArray();
        return (lines.Length > 0 ? lines[0] : null, headers, leftover);
    }

    private static async Task<string> ReadBodyAsync(System.IO.Stream stream, byte[] leftover, int contentLength, CancellationToken ct)
    {
        var body = new byte[contentLength];
        var copied = Math.Min(leftover.Length, contentLength);
        Array.Copy(leftover, body, copied);

        var remaining = contentLength - copied;
        var offset = copied;
        while (remaining > 0)
        {
            var read = await stream.ReadAsync(body.AsMemory(offset, remaining), ct);
            if (read == 0) break;
            offset += read;
            remaining -= read;
        }

        return System.Text.Encoding.UTF8.GetString(body, 0, offset);
    }

    private static async Task WriteResponseAsync(System.IO.Stream stream, string status, string body, CancellationToken ct)
    {
        var bodyBytes = System.Text.Encoding.UTF8.GetBytes(body);
        var header = $"HTTP/1.1 {status}\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n";
        var headerBytes = System.Text.Encoding.ASCII.GetBytes(header);
        await stream.WriteAsync(headerBytes, ct);
        await stream.WriteAsync(bodyBytes, ct);
        await stream.FlushAsync(ct);
    }

    private static (string Route, Dictionary<string, string> Query) ParsePath(string path)
    {
        var qIndex = path.IndexOf('?');
        var route = qIndex >= 0 ? path[..qIndex] : path;
        var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (qIndex >= 0)
        {
            foreach (var pair in path[(qIndex + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = pair.IndexOf('=');
                var key = eq >= 0 ? pair[..eq] : pair;
                var value = eq >= 0 ? Uri.UnescapeDataString(pair[(eq + 1)..]) : "";
                query[key] = value;
            }
        }

        return (route, query);
    }

    /// <summary>Adresse IP locale la plus probable (Wi-Fi/Ethernet), pour l'afficher côté Réglages à recopier sur le téléphone — au mieux, peut être nulle.</summary>
    public static string? GetLocalIpAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            // Ne nécessite pas d'accès réseau réel : une connexion UDP ne fait que résoudre la route locale.
            socket.Connect("8.8.8.8", 65530);
            return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
