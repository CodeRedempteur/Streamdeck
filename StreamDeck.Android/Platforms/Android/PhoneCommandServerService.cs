using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using StreamDeck.Services;

namespace StreamDeck.Mobile;

/// <summary>
/// v1.34.13, demandé explicitement ("les script pc direction le tel") : reçoit des commandes envoyées par un script
/// PC (nouveau bloc « Téléphone » dans Script Builder, voir StreamDeck.ScriptBuilder/Generation/ScriptGenerator.cs)
/// — sonner/alarme (réutilise <see cref="FindPhoneService"/>, le même mécanisme que le bouton/la commande vocale
/// « Retrouver le téléphone ») ou ouvrir une application installée par son paquet. Service au premier plan (comme
/// <see cref="VoiceListenerService"/>) pour continuer à écouter même l'appli fermée.
///
/// <para>
/// <b>Volontairement un petit serveur TCP fait main plutôt que <c>System.Net.HttpListener</c></b> : cette classe lit
/// la ligne de requête et les en-têtes (<c>GET /command?token=...&amp;action=... HTTP/1.1</c>), et répond par un
/// texte brut minimal — pas besoin de plus, PowerShell (<c>Invoke-RestMethod</c>) n'a besoin d'aucune négociation
/// HTTP avancée. Choix délibéré après plusieurs correctifs ratés cette même session sur des bulles d'interface trop
/// élaborées : rester simple plutôt que de dépendre d'une implémentation HTTP complète dont le support réel sur
/// Android n'a pas pu être vérifié ici.
/// </para>
///
/// <para>
/// v1.34.16 ("importconfig", voir <see cref="PhoneCommandServerService.ImportConfig"/>) : la plupart des commandes
/// restent de simples <c>GET</c> (tout dans la chaîne de requête), mais l'envoi d'une configuration complète depuis
/// l'onglet « Téléphone » du PC a besoin d'un corps — donc aussi un <c>POST</c> avec <c>Content-Length</c>, lu en
/// octets exacts (voir <see cref="ReadRequestHeadAsync"/>/<see cref="ReadBodyAsync"/>) plutôt que via le
/// <see cref="System.IO.StreamReader"/> ASCII utilisé jusqu'ici (qui corromprait les accents français d'un corps JSON UTF-8).
/// </para>
///
/// <para>
/// <b>IMPORTANT — jamais testé.</b> Comme <see cref="VoiceListenerService"/>, aucun appareil/émulateur Android
/// disponible pendant l'écriture ; jamais exécuté pour de vrai. À vérifier en conditions réelles : le service au
/// premier plan de type <c>dataSync</c> reste-t-il bien actif en tâche de fond selon le fabricant (même réserve que
/// pour l'écoute vocale continue), et le pare-feu/routeur du réseau local laisse-t-il bien passer une connexion
/// entrante vers le téléphone sur le port choisi (généralement oui sur un simple réseau Wi-Fi domestique, pas garanti
/// partout).
/// </para>
/// </summary>
// v1.34.24 : cause RÉELLE trouvée (merci pour le journal détaillé !) — IllegalArgumentException "foregroundServiceType
// 0x1 is not a subset of foregroundServiceType attribute 0x0 in service element of manifest file". En .NET MAUI, le
// manifeste final est généré à partir des attributs C# [Service(...)], qui l'emportent sur ce qui est écrit à la main
// dans AndroidManifest.xml pour ce genre de métadonnée — d'où l'attribut qui ressortait à 0 malgré la déclaration XML
// "dataSync". Le bon endroit pour le déclarer est donc ICI, via ForegroundServiceType sur l'attribut [Service].
[Service(Exported = false, ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeDataSync)]
public class PhoneCommandServerService : Service
{
    private const string ChannelId = "streamdeck-phone-server";
    private const int NotificationId = 4202;

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private ConfigService? _config;
    private FindPhoneService? _findPhone;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        // v1.34.20, suite à un plantage signalé sur un appareil (Samsung) activant ce service alors qu'un autre
        // appareil ne plantait pas avec la même action : protège tout ce démarrage plutôt que de laisser une
        // exception (fabricant-dépendante, ex. construction de la notification/canal) remonter non rattrapée — et
        // consigne l'erreur dans CrashLog même si elle est rattrapée ici, pour rester diagnosticable (Réglages →
        // « Dernier plantage ») sans avoir besoin d'un vrai plantage complet de l'appli.
        // v1.34.22sexies : instrumentation temporaire (marqueurs sans exception) pour isoler la vraie cause de
        // ForegroundServiceDidNotStartInTimeException — persiste malgré le réordonnancement, la batterie
        // "sans restriction" et un test en Release. On veut savoir : est-ce StartForeground() lui-même qui lève une
        // exception rattrapée en silence, ou bien tout se déroule normalement côté code (pur problème de timing
        // système, hors de notre contrôle direct) ?
        CrashLog.WriteMarker("PhoneCommandServerService.OnStartCommand : entrée");
        try
        {
            Notification notification;
            try
            {
                notification = BuildNotification();
                CrashLog.WriteMarker("PhoneCommandServerService.OnStartCommand : BuildNotification() OK");
            }
            catch (System.Exception exNotif)
            {
                CrashLog.Write("PhoneCommandServerService.BuildNotification", exNotif);
                throw;
            }

            try
            {
                global::AndroidX.Core.App.ServiceCompat.StartForeground(this, NotificationId, notification,
                    (int)global::Android.Content.PM.ForegroundService.TypeDataSync);
                CrashLog.WriteMarker("PhoneCommandServerService.OnStartCommand : ServiceCompat.StartForeground() OK");
            }
            catch (System.Exception exFg)
            {
                CrashLog.Write("PhoneCommandServerService.ServiceCompat.StartForeground", exFg);
                throw;
            }

            var services = MainApplication.CurrentServices;
            _config = services?.GetService(typeof(ConfigService)) as ConfigService;
            _findPhone = services?.GetService(typeof(FindPhoneService)) as FindPhoneService;

            _cts = new CancellationTokenSource();
            _ = Task.Run(() => ListenLoopAsync(_cts.Token));
        }
        catch (System.Exception ex)
        {
            CrashLog.Write("PhoneCommandServerService.OnStartCommand", ex);
            StopSelf();
        }

        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        _cts?.Cancel();
        try { _listener?.Stop(); } catch (System.Exception) { /* déjà arrêté */ }
        _listener = null;
        base.OnDestroy();
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        var port = _config?.Current.PhoneServer.Port ?? 8766;
        try
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
        }
        catch (System.Exception)
        {
            // Port déjà utilisé par une autre appli, ou indisponible : rien de mieux à faire ici que de rester
            // silencieux — le service tourne mais n'accepte jamais rien, comme s'il était désactivé côté PC.
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(ct);
            }
            catch (System.Exception)
            {
                break; // écouteur arrêté (OnDestroy) ou erreur réseau irrécupérable
            }

            _ = Task.Run(() => HandleClientAsync(client, ct), ct);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using var c = client;
        c.ReceiveTimeout = 5000;
        c.SendTimeout = 5000;

        try
        {
            await using System.IO.Stream stream = c.GetStream();

            // v1.34.16 ("importconfig", voir ci-dessous) : lecture en OCTETS BRUTS plutôt que via un StreamReader
            // texte, et volontairement plus via ASCII — un StreamReader ASCII corromprait un corps JSON UTF-8
            // (accents français dans les noms de boutons/espaces). Les en-têtes eux-mêmes sont de l'ASCII pur (donc
            // un sous-ensemble valide d'UTF-8), décodés une fois la fin des en-têtes trouvée.
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
        catch (System.Exception)
        {
            // Client mal formé, déconnecté en cours de route, etc. : sans gravité pour un petit serveur local.
        }
    }

    /// <summary>
    /// Lit la ligne de requête et les en-têtes octet par octet jusqu'à la ligne vide qui les termine (<c>\r\n\r\n</c>),
    /// sans passer par un <see cref="System.IO.StreamReader"/> bufferisé — pour pouvoir ensuite lire le corps (v1.34.16,
    /// "importconfig") en octets exacts sans perdre ceux déjà lus en trop dans un tampon texte. Renvoie aussi les
    /// octets déjà lus APRÈS la fin des en-têtes dans le même paquet réseau (<paramref name="leftoverBody"/> dans le
    /// tuple), à prépendre au corps.
    /// </summary>
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

        // En-têtes : ASCII pur (sous-ensemble d'UTF-8), donc sans risque à décoder ainsi.
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

    /// <summary>
    /// Lit exactement <paramref name="contentLength"/> octets de corps (ceux déjà en main dans
    /// <paramref name="leftover"/>, puis le reste directement depuis le flux), et les décode en UTF-8.
    /// </summary>
    private static async Task<string> ReadBodyAsync(System.IO.Stream stream, byte[] leftover, int contentLength, CancellationToken ct)
    {
        var body = new byte[contentLength];
        var copied = System.Math.Min(leftover.Length, contentLength);
        Array.Copy(leftover, body, copied);

        var remaining = contentLength - copied;
        var offset = copied;
        while (remaining > 0)
        {
            var read = await stream.ReadAsync(body.AsMemory(offset, remaining), ct);
            if (read == 0) break; // connexion coupée avant la fin du corps annoncé
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

    private (string Status, string Body) HandleRequest(string route, IReadOnlyDictionary<string, string> query, string? body)
    {
        var settings = _config?.Current.PhoneServer;
        if (settings is null || !settings.Enabled)
            return ("503 Service Unavailable", "Serveur desactive");

        if (string.IsNullOrEmpty(settings.Token) || query.GetValueOrDefault("token") != settings.Token)
            return ("401 Unauthorized", "Jeton invalide");

        // "Tester la connexion" côté Script Builder (PhoneSection.razor) : valide juste l'adresse/le jeton, sans
        // déclencher quoi que ce soit sur le téléphone — même principe que /_/ping côté server.ps1 (PC).
        if (route == "/ping")
            return ("200 OK", "OK");

        if (route != "/command")
            return ("404 Not Found", "Introuvable");

        var action = query.GetValueOrDefault("action", "");
        return action switch
        {
            "find" => TriggerFind(),
            "openapp" => OpenApp(query.GetValueOrDefault("app", "")),
            "importconfig" => ImportConfig(body),
            _ => ("400 Bad Request", "Action inconnue")
        };
    }

    /// <summary>
    /// v1.34.16, demandé explicitement ("créer des config tel depuis le pc et l'importer sur le tel") : reçoit une
    /// configuration complète envoyée par l'onglet « Téléphone » du PC (voir PhoneConfigTransferService côté
    /// StreamDeck.ScriptBuilder) et l'applique — réutilise tel quel <see cref="StreamDeck.Models.ConfigTransfer"/>,
    /// déjà utilisé par l'import/export manuel (copier-coller) de BackupSection, en mode Remplacer (même principe
    /// qu'un import classique). Le token local non envoyé par le PC (export sans secrets, par défaut) est préservé
    /// par <see cref="StreamDeck.Models.ConfigTransfer.Apply"/> lui-même.
    /// </summary>
    private (string, string) ImportConfig(string? body)
    {
        if (_config is null) return ("500 Internal Server Error", "Service indisponible");

        var imported = StreamDeck.Models.ConfigTransfer.Parse(body, out var error);
        if (imported is null) return ("400 Bad Request", error);

        try
        {
            var target = _config.Current.Clone();
            var result = StreamDeck.Models.ConfigTransfer.Apply(target, imported, StreamDeck.Models.ImportMode.Replace);
            _config.Save(target);
            return result.Ok ? ("200 OK", result.Message) : ("500 Internal Server Error", result.Message);
        }
        catch (System.Exception ex)
        {
            return ("500 Internal Server Error", ex.Message);
        }
    }

    private (string, string) TriggerFind()
    {
        if (_findPhone is null) return ("500 Internal Server Error", "Service indisponible");
        var result = _findPhone.Trigger();
        return result.Ok ? ("200 OK", "OK") : ("500 Internal Server Error", result.Message);
    }

    private (string, string) OpenApp(string package)
    {
        if (string.IsNullOrWhiteSpace(package)) return ("400 Bad Request", "Parametre app manquant");

        try
        {
            var context = Android.App.Application.Context;
            var intent = context.PackageManager?.GetLaunchIntentForPackage(package);
            if (intent is null) return ("404 Not Found", "Application introuvable (paquet inconnu ou non lancable)");

            intent.AddFlags(ActivityFlags.NewTask);
            context.StartActivity(intent);
            return ("200 OK", "OK");
        }
        catch (System.Exception ex)
        {
            return ("500 Internal Server Error", ex.Message);
        }
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
                var value = eq >= 0 ? System.Uri.UnescapeDataString(pair[(eq + 1)..]) : "";
                query[key] = value;
            }
        }

        return (route, query);
    }

    private Notification BuildNotification()
    {
        var manager = (NotificationManager)GetSystemService(NotificationService)!;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O && manager.GetNotificationChannel(ChannelId) is null)
        {
            var channel = new NotificationChannel(ChannelId, "Commandes du PC", NotificationImportance.Low)
            {
                Description = "Reste affichée tant que le téléphone peut recevoir des commandes du PC."
            };
            manager.CreateNotificationChannel(channel);
        }

        // v1.34.20 : ApplicationInfo.Icon peut renvoyer 0 sur certains appareils/versions (observé comme piste
        // possible pour un plantage spécifique à un appareil) — SetSmallIcon(0) lève alors une exception plutôt que
        // d'afficher une icône par défaut. Repli sur une icône système toujours valide dans ce cas.
        var iconRes = ApplicationInfo?.Icon ?? 0;
        if (iconRes == 0) iconRes = global::Android.Resource.Drawable.IcDialogInfo;

        return new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle("Stream Deck : commandes du PC activées")
            .SetContentText("Le téléphone peut recevoir des commandes depuis un script du PC.")
            .SetSmallIcon(iconRes)
            .SetOngoing(true)
            .SetPriority(NotificationCompat.PriorityLow)
            .Build()!;
    }
}
