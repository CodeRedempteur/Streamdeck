using StreamDeck.Components.Shared;

namespace StreamDeck.Services;

/// <summary>
/// Liste des fuseaux horaires connus de la machine (<see cref="TimeZoneInfo.GetSystemTimeZones"/>), prête à passer à
/// un <c>ChoiceField</c>/<c>ChoicePicker</c> — accepte aussi bien un identifiant Windows ("Tokyo Standard Time")
/// qu'IANA selon la plateforme, les deux fonctionnent avec <see cref="TimeZoneInfo.FindSystemTimeZoneById"/> sur
/// .NET moderne. Partagée (v1.34.6) entre le widget de bureau (StreamDeck.ScriptBuilder, bloc "worldclock") et
/// l'appli (StreamDeck.Android/StreamDeck.Desktop, bouton "Affichage plein écran", InfoKind.WorldClock) pour ne pas
/// dupliquer la table de pays ci-dessous.
///
/// Recherche par pays : .NET ne fournit pas d'association fuseau -> pays, donc le nom du pays est deviné à partir
/// d'un mot-clé (ville/région) trouvé dans le nom affiché du fuseau, via <see cref="CountryHints"/> — posé comme
/// sous-titre (cherché par ChoicePicker). Couvre les pays les plus courants (Europe, grandes places d'affaires), pas
/// exhaustif : un fuseau sans mot-clé reconnu reste cherchable par ville/région (le nom affiché lui-même), juste pas
/// par pays.
/// </summary>
public static class TimeZoneChoices
{
    private static readonly (string Keyword, string Country)[] CountryHints =
    {
        ("Zurich", "Suisse"), ("Geneva", "Suisse"), ("Genève", "Suisse"), ("Bern", "Suisse"),
        ("Paris", "France"), ("Berlin", "Allemagne"), ("Rome", "Italie"), ("Madrid", "Espagne"),
        ("Lisbon", "Portugal"), ("London", "Royaume-Uni"), ("Dublin", "Irlande"), ("Amsterdam", "Pays-Bas"),
        ("Brussels", "Belgique"), ("Vienna", "Autriche"), ("Wien", "Autriche"), ("Prague", "Tchéquie"),
        ("Warsaw", "Pologne"), ("Budapest", "Hongrie"), ("Stockholm", "Suède"), ("Oslo", "Norvège"),
        ("Copenhagen", "Danemark"), ("Helsinki", "Finlande"), ("Athens", "Grèce"), ("Moscow", "Russie"),
        ("Istanbul", "Turquie"), ("Kyiv", "Ukraine"), ("Bucharest", "Roumanie"), ("Sofia", "Bulgarie"),
        ("New York", "États-Unis"), ("Eastern", "États-Unis"), ("Central", "États-Unis"), ("Mountain", "États-Unis"),
        ("Pacific", "États-Unis"), ("Los Angeles", "États-Unis"), ("Chicago", "États-Unis"), ("Denver", "États-Unis"),
        ("Toronto", "Canada"), ("Vancouver", "Canada"), ("Mexico", "Mexique"),
        ("São Paulo", "Brésil"), ("Sao Paulo", "Brésil"), ("Buenos Aires", "Argentine"), ("Santiago", "Chili"),
        ("Tokyo", "Japon"), ("Seoul", "Corée du Sud"), ("Shanghai", "Chine"), ("Beijing", "Chine"),
        ("Hong Kong", "Hong Kong"), ("Taipei", "Taïwan"), ("Singapore", "Singapour"), ("Bangkok", "Thaïlande"),
        ("Jakarta", "Indonésie"), ("Kuala Lumpur", "Malaisie"), ("Manila", "Philippines"), ("Hanoi", "Vietnam"),
        ("Mumbai", "Inde"), ("Kolkata", "Inde"), ("Delhi", "Inde"), ("Dubai", "Émirats arabes unis"),
        ("Riyadh", "Arabie saoudite"), ("Tel Aviv", "Israël"), ("Jerusalem", "Israël"), ("Cairo", "Égypte"),
        ("Johannesburg", "Afrique du Sud"), ("Nairobi", "Kenya"), ("Lagos", "Nigeria"), ("Casablanca", "Maroc"),
        ("Sydney", "Australie"), ("Melbourne", "Australie"), ("Perth", "Australie"), ("Auckland", "Nouvelle-Zélande"),
        ("Reykjavik", "Islande"), ("UTC", "International")
    };

    private static string? GuessCountry(string displayName) =>
        CountryHints.FirstOrDefault(h => displayName.Contains(h.Keyword, StringComparison.OrdinalIgnoreCase)).Country;

    private static readonly Lazy<IReadOnlyList<ChoiceItem>> AllLazy = new(() =>
        TimeZoneInfo.GetSystemTimeZones().Select(tz => new ChoiceItem(tz.Id, tz.DisplayName, GuessCountry(tz.DisplayName), "fa-solid fa-earth-americas")).ToList());

    /// <summary>Tous les fuseaux de la machine, calculés une seule fois (ne changent pas en cours d'exécution).</summary>
    public static IReadOnlyList<ChoiceItem> All => AllLazy.Value;
}
