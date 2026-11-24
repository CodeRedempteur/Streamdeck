using System.Security.Cryptography;
using StreamDeck.Models;

namespace StreamDeck.Services;

public record PinCheck(bool Ok, TimeSpan? LockedFor);

/// <summary>Code PIN : stocké haché (PBKDF2), avec une pause après plusieurs essais ratés.</summary>
public class SecurityService
{
    private const int Iterations = 100_000;
    private const int HashBytes = 32;
    private const int SaltBytes = 16;
    private const int MaxFailures = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromSeconds(30);

    private readonly object _lock = new();
    private int _failures;
    private DateTime _lockedUntil = DateTime.MinValue;

    public static bool IsValidPin(string? pin) =>
        pin is not null
        && pin.Length >= SecuritySettings.MinPinLength
        && pin.Length <= SecuritySettings.MaxPinLength
        && pin.All(char.IsAsciiDigit);

    public void SetPin(SecuritySettings settings, string pin)
    {
        if (!IsValidPin(pin))
            throw new ArgumentException($"Le code PIN doit contenir {SecuritySettings.MinPinLength} à {SecuritySettings.MaxPinLength} chiffres.");

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        settings.PinSalt = Convert.ToBase64String(salt);
        settings.PinHash = Convert.ToBase64String(Derive(pin, salt));
    }

    public void ClearPin(SecuritySettings settings)
    {
        settings.PinSalt = string.Empty;
        settings.PinHash = string.Empty;
        settings.LockSettings = false;
    }

    /// <summary>Vérifie un code. Après 5 erreurs de suite, plus aucun essai n'est accepté pendant 30 secondes.</summary>
    public PinCheck Check(SecuritySettings settings, string pin)
    {
        lock (_lock)
        {
            if (DateTime.UtcNow < _lockedUntil)
                return new PinCheck(false, _lockedUntil - DateTime.UtcNow);

            if (!settings.HasPin)
                return new PinCheck(false, null);

            try
            {
                var expected = Convert.FromBase64String(settings.PinHash);
                var actual = Derive(pin, Convert.FromBase64String(settings.PinSalt));

                if (CryptographicOperations.FixedTimeEquals(expected, actual))
                {
                    _failures = 0;
                    return new PinCheck(true, null);
                }
            }
            catch (FormatException)
            {
                // Données de PIN abîmées : traité comme un code faux.
            }

            if (++_failures >= MaxFailures)
            {
                _failures = 0;
                _lockedUntil = DateTime.UtcNow + LockDuration;
                return new PinCheck(false, LockDuration);
            }

            return new PinCheck(false, null);
        }
    }

    private static byte[] Derive(string pin, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
}
