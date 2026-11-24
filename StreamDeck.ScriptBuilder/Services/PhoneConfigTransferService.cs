using StreamDeck.Models;

namespace StreamDeck.ScriptBuilder.Services;

/// <summary>
/// v1.34.16, onglet « Téléphone » (voir PhoneTemplates.razor) : envoie un modèle composé sur le PC vers le
/// téléphone, en réseau direct ("Réseau direct" explicitement demandé plutôt qu'un fichier à transférer à la main)
/// — réutilise l'adresse/port/jeton d'un téléphone déjà configuré pour le bloc de script « Téléphone »
/// (<see cref="BuilderConfig.PhoneTargets"/>, Réglages → Téléphone) et le petit serveur du téléphone
/// (PhoneCommandServerService), étendu pour cette version pour accepter une configuration complète
/// (POST + corps JSON, action "importconfig") en plus des commandes existantes (sonner, ouvrir une application).
///
/// <para>
/// <b>Sens PC → téléphone seulement pour l'instant.</b> Le sens inverse (le téléphone envoie sa configuration au
/// PC) demande un petit serveur équivalent qui écoute côté PC, qui n'existe pas encore — prévu pour une prochaine
/// version plutôt que improvisé ici sans avoir pu étudier comment il doit cohabiter avec le serveur de scripts
/// existant (StreamDeck.ScriptBuilder/Services/ServerFolderLocator.cs et consorts).
/// </para>
///
/// <para><b>Non vérifié</b> (comme le reste de ce qui touche au petit serveur du téléphone) : aucun appareil Android
/// disponible ici pour tester un vrai envoi.</para>
/// </summary>
public class PhoneConfigTransferService
{
    public async Task<(bool Ok, string Message)> ExportAsync(PhoneTarget target, DeckConfig config, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(target.IpAddress))
            return (false, "Adresse IP du téléphone manquante (Réglages → Téléphone).");
        if (string.IsNullOrWhiteSpace(target.Token))
            return (false, "Jeton manquant (Réglages → Téléphone).");

        var json = ConfigTransfer.Export(config, includeTokens: false);
        var url = $"http://{target.IpAddress}:{target.Port}/command?token={Uri.EscapeDataString(target.Token)}&action=importconfig";

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        try
        {
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            using var response = await http.PostAsync(url, content, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            return response.IsSuccessStatusCode
                ? (true, string.IsNullOrWhiteSpace(text) ? "Envoyé au téléphone." : text)
                : (false, $"Le téléphone a refusé ({(int)response.StatusCode}) : {text}");
        }
        catch (System.Exception ex)
        {
            return (false, $"Connexion impossible : {ex.Message}");
        }
    }
}
