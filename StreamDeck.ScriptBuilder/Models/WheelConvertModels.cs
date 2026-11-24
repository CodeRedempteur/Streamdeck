namespace StreamDeck.ScriptBuilder.Models;

/// <summary>
/// v1.38, demandé explicitement ("dans les action je puisse ajouté des action du stream deck dans la roue directement
/// config depuis mon app streamdeck" + "autant de wheel que souhaité avec facilement possibilité de switch de roue") :
/// une roue de WheelConvert (roue radiale Windows, Shift + clic droit maintenu). Publiée automatiquement par
/// <see cref="Services.WheelBridgeService"/> dans %AppData%\WheelConvert\providers\streamdeck.json à chaque
/// modification de la configuration ; WheelConvert l'affiche en direct, on passe d'une roue à l'autre à la molette.
/// </summary>
public class WheelConvertWheel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Roue";
    public string Icon { get; set; } = "fa-solid fa-circle-notch";
    public string Color { get; set; } = "#3E63DD";
    public List<WheelConvertItem> Items { get; set; } = new();

    public WheelConvertWheel Clone() => new()
    {
        Id = Id, Name = Name, Icon = Icon, Color = Color,
        Items = Items.Select(i => i.Clone()).ToList()
    };
}

/// <summary>Un secteur d'une roue : un script de la bibliothèque, ou un lien vers une autre roue.</summary>
public class WheelConvertItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>"script" (lance <see cref="ScriptName"/> en local, comme le widget) ou "wheel" (ouvre <see cref="TargetWheelId"/>).</summary>
    public string Kind { get; set; } = "script";

    public string ScriptName { get; set; } = "";
    public string? TargetWheelId { get; set; }

    /// <summary>Nom affiché dans la roue — vide = nom du script (ou de la roue visée).</summary>
    public string Label { get; set; } = "";

    /// <summary>Icône/couleur propres — vides = celles du script dans la bibliothèque (ou de la roue visée).</summary>
    public string Icon { get; set; } = "";
    public string Color { get; set; } = "";

    public WheelConvertItem Clone() => new()
    {
        Id = Id, Kind = Kind, ScriptName = ScriptName, TargetWheelId = TargetWheelId,
        Label = Label, Icon = Icon, Color = Color
    };
}
