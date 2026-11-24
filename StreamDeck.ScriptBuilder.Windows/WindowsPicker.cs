using StreamDeck.ScriptBuilder.Services;

namespace StreamDeck.ScriptBuilderApp;

/// <summary>Fenêtres « Ouvrir » / « Choisir un dossier » de Windows.</summary>
internal sealed class WindowsPicker : INativePicker
{
    public Task<string?> PickFolderAsync() => MainThread.InvokeOnMainThreadAsync(async () =>
    {
        var picker = new global::Windows.Storage.Pickers.FolderPicker();
        picker.FileTypeFilter.Add("*");
        Attach(picker);

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    });

    public Task<string?> PickFileAsync(IReadOnlyCollection<string>? extensions) => MainThread.InvokeOnMainThreadAsync(async () =>
    {
        var picker = new global::Windows.Storage.Pickers.FileOpenPicker();
        if (extensions is { Count: > 0 })
            foreach (var extension in extensions) picker.FileTypeFilter.Add(extension);
        else
            picker.FileTypeFilter.Add("*");
        Attach(picker);

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    });

    // Une application WinUI 3 doit relier le sélecteur à sa fenêtre, sinon Windows refuse de l'ouvrir.
    private static void Attach(object picker)
    {
        var window = Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window
                     ?? throw new InvalidOperationException("Fenêtre principale introuvable.");

        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
    }
}
