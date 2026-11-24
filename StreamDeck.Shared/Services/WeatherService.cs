using System.Globalization;
using System.Text.Json;

namespace StreamDeck.Services;

/// <summary>Une région trouvée par <see cref="WeatherService.SearchLocationsAsync"/> — le nom affiché et les
/// coordonnées à enregistrer sur le bloc "weather" (widget) ou le bouton "Affichage plein écran" (appli).</summary>
public class WeatherLocationResult
{
    public string Name { get; set; } = "";
    public string? Admin1 { get; set; }
    public string? Country { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>"Genève, Genève, Suisse" — les parties vides (région ou pays manquant selon le lieu) sont omises.</summary>
    public string DisplayName => string.Join(", ", new[] { Name, Admin1, Country }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());
}

/// <summary>Relevé météo courant — jamais d'exception qui remonte à l'appelant.</summary>
public class WeatherResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public double TemperatureC { get; set; }
    public int WeatherCode { get; set; }
    public double WindKmh { get; set; }
    public bool IsDay { get; set; } = true;
}

/// <summary>
/// Recherche une région et relève la météo courante via <b>Open-Meteo</b> (open-meteo.com) — choisi parce qu'il ne
/// demande ni clé ni inscription (contrairement à la plupart des autres services météo), ce qui évite de demander à
/// l'utilisateur de créer un compte tiers et de coller une clé API juste pour afficher la météo. Deux points d'entrée :
/// la recherche de région (geocoding-api.open-meteo.com, résout un nom tapé en coordonnées) et le relevé courant
/// (api.open-meteo.com, à partir de ces coordonnées).
/// </summary>
/// <remarks>
/// Vit dans StreamDeck.Shared (depuis v1.34.6, déplacé depuis StreamDeck.ScriptBuilder) pour être utilisable à la
/// fois par le widget de bureau (StreamDeck.ScriptBuilder) ET par l'appli (StreamDeck.Android/StreamDeck.Desktop,
/// bouton "Affichage plein écran" — voir InfoDisplayOverlay.razor, InfoKind.Weather). <b>Non vérifié</b> : ce bac à
/// sable n'a pas d'accès réseau sortant vers ces domaines, donc écrit d'après la documentation publique d'Open-Meteo
/// (stable et largement utilisée) mais jamais appelé pour de vrai ici — à tester sur un vrai appareil connecté à
/// internet.
/// </remarks>
public class WeatherService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly Dictionary<string, (DateTime Fetched, WeatherResult Result)> _cache = new();

    public async Task<List<WeatherLocationResult>> SearchLocationsAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return new();
        try
        {
            var url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(query.Trim())}&count=6&language=fr&format=json";
            using var response = await Http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return new();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var list = new List<WeatherLocationResult>();
            if (doc.RootElement.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
            {
                foreach (var r in results.EnumerateArray())
                {
                    list.Add(new WeatherLocationResult
                    {
                        Name = r.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                        Admin1 = r.TryGetProperty("admin1", out var a) ? a.GetString() : null,
                        Country = r.TryGetProperty("country", out var c) ? c.GetString() : null,
                        Latitude = r.TryGetProperty("latitude", out var lat) ? lat.GetDouble() : 0,
                        Longitude = r.TryGetProperty("longitude", out var lon) ? lon.GetDouble() : 0
                    });
                }
            }
            return list;
        }
        catch (Exception)
        {
            return new(); // pas de connexion / service indisponible : liste vide plutôt qu'une exception affichée telle quelle
        }
    }

    /// <summary>Relevé courant pour des coordonnées données — mis en cache 10 minutes (par coordonnées arrondies) pour
    /// ne pas ré-interroger le service à chaque redessin (le widget ou l'écran plein écran se redessinent souvent).</summary>
    public async Task<WeatherResult> GetCurrentAsync(double latitude, double longitude, CancellationToken ct = default)
    {
        var key = $"{latitude:0.###},{longitude:0.###}";
        if (_cache.TryGetValue(key, out var cached) && DateTime.UtcNow - cached.Fetched < TimeSpan.FromMinutes(10))
            return cached.Result;

        try
        {
            var lat = latitude.ToString(CultureInfo.InvariantCulture);
            var lon = longitude.ToString(CultureInfo.InvariantCulture);
            var url = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,weather_code,wind_speed_10m,is_day&timezone=auto";
            using var response = await Http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return new WeatherResult { Error = $"Le service météo a répondu une erreur ({(int)response.StatusCode})." };

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var current = doc.RootElement.GetProperty("current");
            var result = new WeatherResult
            {
                Success = true,
                TemperatureC = current.GetProperty("temperature_2m").GetDouble(),
                WeatherCode = current.GetProperty("weather_code").GetInt32(),
                WindKmh = current.TryGetProperty("wind_speed_10m", out var w) ? w.GetDouble() : 0,
                IsDay = !current.TryGetProperty("is_day", out var d) || d.GetInt32() == 1
            };
            _cache[key] = (DateTime.UtcNow, result);
            return result;
        }
        catch (Exception ex)
        {
            return new WeatherResult { Error = ex.Message };
        }
    }

    /// <summary>Traduit un code météo standard OMM (WMO, celui que renvoie Open-Meteo) en description française +
    /// icône Font Awesome — voir la table officielle sur open-meteo.com/en/docs (section WMO Weather interpretation codes).</summary>
    public static (string Description, string Icon) Describe(int code, bool isDay) => code switch
    {
        0 => ("Ciel dégagé", isDay ? "fa-sun" : "fa-moon"),
        1 => ("Plutôt dégagé", isDay ? "fa-cloud-sun" : "fa-cloud-moon"),
        2 => ("Partiellement nuageux", isDay ? "fa-cloud-sun" : "fa-cloud-moon"),
        3 => ("Couvert", "fa-cloud"),
        45 or 48 => ("Brouillard", "fa-smog"),
        51 or 53 or 55 => ("Bruine", "fa-cloud-rain"),
        56 or 57 => ("Bruine verglaçante", "fa-cloud-rain"),
        61 or 63 or 65 => ("Pluie", "fa-cloud-showers-heavy"),
        66 or 67 => ("Pluie verglaçante", "fa-cloud-showers-heavy"),
        71 or 73 or 75 => ("Neige", "fa-snowflake"),
        77 => ("Grains de neige", "fa-snowflake"),
        80 or 81 or 82 => ("Averses", "fa-cloud-showers-heavy"),
        85 or 86 => ("Averses de neige", "fa-snowflake"),
        95 => ("Orage", "fa-cloud-bolt"),
        96 or 99 => ("Orage avec grêle", "fa-cloud-bolt"),
        _ => ("Conditions inconnues", "fa-cloud")
    };
}
