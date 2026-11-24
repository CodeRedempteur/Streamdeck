namespace StreamDeck.Components.Shared;

/// <summary>Un choix proposé dans une liste (ouverte dans une modale au milieu de l'écran).</summary>
public record ChoiceItem(string Value, string Title, string? Subtitle = null, string? Icon = null);
