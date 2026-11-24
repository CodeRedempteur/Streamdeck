namespace StreamDeck.ScriptBuilder.Services;

/// <summary>Ce qui dépend de l'hôte (dossier de config…). Implémenté par l'application Windows.</summary>
public interface IBuilderPlatform
{
    string ConfigDirectory { get; }
}
