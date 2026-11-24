using System.Security.Cryptography;
using System.Text;

namespace StreamDeck.ScriptBuilder.Models;

/// <summary>
/// Protège le mot de passe d'une boîte mail avec la DPAPI de Windows (<c>ProtectedData</c>) au lieu de le stocker en
/// clair dans config.json — lié à l'utilisateur Windows courant : illisible si le fichier de config est copié sur
/// une autre machine ou un autre compte (il faudra alors ressaisir le mot de passe, l'application le signalera comme
/// « identifiants incomplets » plutôt que de planter). Non vérifié sur un vrai Windows dans cet environnement (pas de
/// DPAPI disponible hors Windows), mais <c>System.Security.Cryptography.ProtectedData</c> est l'API standard et
/// documentée pour ça depuis toujours sur .NET.
/// </summary>
public static class MailCredentialProtector
{
    private const string Prefix = "dpapi:";

    public static string Protect(string plainPassword)
    {
        if (string.IsNullOrEmpty(plainPassword)) return "";
        try
        {
            var bytes = Encoding.UTF8.GetBytes(plainPassword);
            var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return Prefix + Convert.ToBase64String(protectedBytes);
        }
        catch (Exception)
        {
            // Dégradation : mieux vaut garder le mot de passe utilisable (en clair) que de le perdre silencieusement.
            return plainPassword;
        }
    }

    public static string Unprotect(string stored)
    {
        if (string.IsNullOrEmpty(stored)) return "";
        if (!stored.StartsWith(Prefix, StringComparison.Ordinal)) return stored; // pas protégé (dégradation ci-dessus) : renvoyé tel quel

        try
        {
            var protectedBytes = Convert.FromBase64String(stored[Prefix.Length..]);
            var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception)
        {
            return ""; // illisible (autre machine/compte, ou corrompu) : à ressaisir dans les Réglages
        }
    }
}
