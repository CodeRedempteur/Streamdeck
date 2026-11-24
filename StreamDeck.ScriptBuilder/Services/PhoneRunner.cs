using StreamDeck.Services;

namespace StreamDeck.ScriptBuilder.Services;

/// <summary>
/// v1.34.13 : teste la connexion à un <see cref="PhoneTarget"/> (bouton « Tester la connexion » côté Réglages) — même
/// principe que <c>ScriptRunner.TestAsync</c> pour un PC, mais vers le petit serveur du téléphone
/// (<c>PhoneCommandServerService</c> côté Android, route "/ping" — ne déclenche rien sur le téléphone, juste une
/// vérification d'adresse/jeton). La génération réelle des scripts PowerShell (bloc « Téléphone ») vit dans
/// ScriptGenerator.cs/BuilderHelpers.cs — ce service ne sert qu'au test depuis l'interface.
/// </summary>
public class PhoneRunner
{
    // Même principe que ScriptRunner.Http (StreamDeck.Shared) : un champ statique plutôt qu'un HttpClient injecté
    // via DI, non enregistré dans ce projet.
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public async Task<RunResult> TestAsync(PhoneTarget phone, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(phone.IpAddress))
            return new RunResult(false, "Adresse IP manquante");

        var url = $"http://{phone.IpAddress.Trim()}:{phone.Port}/ping?token={Uri.EscapeDataString(phone.Token)}";
        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            return new RunResult(false, "Adresse invalide");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));

            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.IsSuccessStatusCode) return new RunResult(true, "Téléphone joignable");
            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                return new RunResult(false, "Téléphone joignable, mais jeton refusé");
            if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                return new RunResult(false, "Téléphone joignable, mais la réception de commandes est désactivée sur l'appli");
            return new RunResult(false, $"Réponse inattendue ({(int)response.StatusCode})");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new RunResult(false, "Pas de réponse (délai dépassé) — vérifie l'adresse IP, le port, et que le téléphone est sur le même réseau");
        }
        catch (Exception)
        {
            return new RunResult(false, "Téléphone injoignable");
        }
    }
}
