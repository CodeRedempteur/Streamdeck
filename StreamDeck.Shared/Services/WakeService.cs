using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace StreamDeck.Services;

/// <summary>Réveille un PC par Wake-on-LAN : envoi d'un « magic packet » UDP en broadcast sur le réseau local.</summary>
public class WakeService
{
    private static readonly int[] Ports = { 9, 7 };

    /// <summary>Accepte 6C-02-E0-3E-35-7E, 6C:02:E0:3E:35:7E, 6c02.e03e.357e ou 6C02E03E357E.</summary>
    public static bool TryParseMac(string? text, out byte[] mac)
    {
        mac = Array.Empty<byte>();

        var input = text ?? string.Empty;
        var hasInvalidChar = input.Any(c => !Uri.IsHexDigit(c) && c is not ('-' or ':' or '.' or ' '));
        var hex = new string(input.Where(Uri.IsHexDigit).ToArray());
        if (hasInvalidChar || hex.Length != 12) return false;

        mac = new byte[6];
        for (var i = 0; i < 6; i++)
            mac[i] = byte.Parse(hex.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        return true;
    }

    /// <summary>6 octets 0xFF puis l'adresse MAC répétée 16 fois (102 octets).</summary>
    public static byte[] BuildMagicPacket(byte[] mac)
    {
        var packet = new byte[6 + 16 * 6];
        for (var i = 0; i < 6; i++) packet[i] = 0xFF;
        for (var i = 0; i < 16; i++) Buffer.BlockCopy(mac, 0, packet, 6 + i * 6, 6);
        return packet;
    }

    /// <summary>Envoie le paquet de réveil. Ne lève jamais d'exception.</summary>
    /// <param name="mac">Adresse MAC de la carte réseau du PC.</param>
    /// <param name="serverUrl">Adresse du serveur du PC : sert à deviner le broadcast du réseau (ex : 192.168.1.255).</param>
    public async Task<RunResult> WakeAsync(string? mac, string? serverUrl, CancellationToken ct = default)
    {
        if (!TryParseMac(mac, out var bytes))
            return new RunResult(false, "Adresse MAC invalide (voir Réglages)");

        var packet = BuildMagicPacket(bytes);
        var sent = 0;

        try
        {
            // 1) Envoi générique : broadcast global + broadcast du réseau du serveur.
            var generic = new HashSet<IPAddress> { IPAddress.Broadcast };
            var fromServer = DirectedBroadcastOf(serverUrl);
            if (fromServer is not null) generic.Add(fromServer);

            sent += await SendAsync(null, generic, packet, ct);

            // 2) Envoi par interface réseau (utile quand il y a plusieurs cartes : VirtualBox, VMware, 4G + Wi-Fi…).
            foreach (var (local, broadcast) in LocalBroadcasts())
                sent += await SendAsync(local, new[] { broadcast }, packet, ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // On tombe dans le contrôle ci-dessous.
        }

        return sent > 0
            ? new RunResult(true, "Paquet de réveil envoyé")
            : new RunResult(false, "Envoi impossible : appareil non connecté au réseau ?");
    }

    private static async Task<int> SendAsync(IPAddress? local, IEnumerable<IPAddress> targets, byte[] packet, CancellationToken ct)
    {
        var sent = 0;

        try
        {
            using var udp = local is null
                ? new UdpClient(AddressFamily.InterNetwork)
                : new UdpClient(new IPEndPoint(local, 0));
            udp.EnableBroadcast = true;

            foreach (var target in targets)
            foreach (var port in Ports)
            {
                try
                {
                    await udp.SendAsync(packet, packet.Length, new IPEndPoint(target, port));
                    sent++;
                }
                catch (Exception) when (!ct.IsCancellationRequested)
                {
                    // Cette destination n'est pas joignable depuis cette interface : on essaie les autres.
                }
            }
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Interface inutilisable : ignorée.
        }

        return sent;
    }

    /// <summary>Broadcast /24 déduit de l'adresse du serveur, ex : http://192.168.1.100:8765 -> 192.168.1.255.</summary>
    private static IPAddress? DirectedBroadcastOf(string? serverUrl)
    {
        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri)) return null;
        if (!IPAddress.TryParse(uri.Host, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork) return null;

        var bytes = ip.GetAddressBytes();
        bytes[3] = 255;
        return new IPAddress(bytes);
    }

    /// <summary>Adresse locale + broadcast de chaque interface IPv4 active (au mieux : peut échouer sur certains Android).</summary>
    private static List<(IPAddress Local, IPAddress Broadcast)> LocalBroadcasts()
    {
        var result = new List<(IPAddress, IPAddress)>();

        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;

                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (ua.IPv4Mask is null) continue;

                    var ip = ua.Address.GetAddressBytes();
                    var mask = ua.IPv4Mask.GetAddressBytes();
                    var broadcast = new byte[4];
                    for (var i = 0; i < 4; i++) broadcast[i] = (byte)(ip[i] | ~mask[i]);

                    // Adresses auto-attribuées (169.254.x.x) : pas un vrai réseau.
                    if (ip[0] == 169 && ip[1] == 254) continue;

                    result.Add((ua.Address, new IPAddress(broadcast)));
                }
            }
        }
        catch (Exception)
        {
            // Lecture des interfaces refusée (Android récent) : l'envoi générique suffit le plus souvent.
        }

        return result;
    }
}
