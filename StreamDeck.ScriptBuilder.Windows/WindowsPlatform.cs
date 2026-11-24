using StreamDeck.ScriptBuilder.Services;

namespace StreamDeck.ScriptBuilderApp;

internal sealed class WindowsPlatform : IBuilderPlatform
{
    // %APPDATA%\StreamDeckScriptBuilder : contient config.json (dossier du serveur, PowerShell utilisé).
    public string ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StreamDeckScriptBuilder");
}
