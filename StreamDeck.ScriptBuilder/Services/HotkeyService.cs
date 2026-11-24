using StreamDeck.ScriptBuilder.Models;

namespace StreamDeck.ScriptBuilder.Services;

/// <summary>
/// Relie les raccourcis clavier globaux réglés dans les Réglages (<see cref="BuilderConfig.Hotkeys"/>) à
/// <see cref="IGlobalHotkeyService"/> : réenregistre tout auprès de Windows chaque fois que la configuration change,
/// et exécute l'action voulue quand l'un d'eux se déclenche. Un singleton : il suffit qu'un composant l'injecte une
/// fois (voir <c>MainLayout.razor</c>) pour que Windows l'active dès le démarrage de l'application, avant même
/// d'avoir ouvert les Réglages.
/// </summary>
public class HotkeyService
{
    private readonly IGlobalHotkeyService _hotkeys;
    private readonly BuilderConfigService _config;
    private readonly ScriptStore _store;
    private readonly MacroControlService _macroControl;
    private readonly ServerRestarter _restarter;
    private readonly ScriptTester _tester;

    /// <summary>Dernier résultat d'une action déclenchée par raccourci, pour un éventuel affichage (widget, Réglages).</summary>
    public event Action<string>? ActionRan;

    public HotkeyService(IGlobalHotkeyService hotkeys, BuilderConfigService config, ScriptStore store,
        MacroControlService macroControl, ServerRestarter restarter, ScriptTester tester)
    {
        _hotkeys = hotkeys;
        _config = config;
        _store = store;
        _macroControl = macroControl;
        _restarter = restarter;
        _tester = tester;

        _hotkeys.Triggered += OnTriggered;
        _config.Changed += Sync;
        Sync();
    }

    /// <summary>Réenregistre tous les raccourcis actifs (appelé automatiquement à chaque changement des Réglages).</summary>
    public void Sync()
    {
        if (!_hotkeys.CanRegister) return;

        _hotkeys.UnregisterAll();
        foreach (var binding in _config.Current.Hotkeys)
        {
            if (!binding.Enabled) continue;
            var combo = HotkeyKeys.Parse(binding.Keys);
            if (combo is not null) _hotkeys.Register(binding.Id, combo);
        }
    }

    private void OnTriggered(string id)
    {
        var binding = _config.Current.Hotkeys.FirstOrDefault(h => h.Id == id);
        if (binding is null || !binding.Enabled) return;

        // Jamais sur le thread qui a reçu l'évènement clavier (message Windows) : une action lente (redémarrer le
        // serveur) le bloquerait, et avec lui la réception d'autres raccourcis ou touches pendant ce temps.
        _ = Task.Run(() => RunAsync(binding));
    }

    private async Task RunAsync(HotkeyBinding binding)
    {
        try
        {
            switch (binding.Action)
            {
                case HotkeyAction.StopAllMacros:
                    _macroControl.StopAllMacros();
                    ActionRan?.Invoke("Toutes les macros en cours ont été arrêtées.");
                    break;

                case HotkeyAction.RestartServer:
                    var restart = await _restarter.RestartAsync();
                    ActionRan?.Invoke(restart.Message);
                    break;

                case HotkeyAction.TriggerScript:
                    var name = binding.ScriptName.Trim();
                    var info = _store.List().FirstOrDefault(s => s.Id.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (info is null) { ActionRan?.Invoke($"« {name} » est introuvable : raccourci sans effet."); break; }
                    if (!info.Enabled) { ActionRan?.Invoke($"« {name} » est désactivé : raccourci sans effet."); break; }

                    // Fire-and-forget côté appelant (le raccourci ne doit pas attendre la fin du script, comme un
                    // appui sur le Stream Deck), mais RunAsync lui-même attend son script ici, dans cette tâche
                    // d'arrière-plan dédiée : rien d'autre n'est bloqué en attendant.
                    var result = await _tester.RunAsync(info.Path);
                    ActionRan?.Invoke(result.Started ? $"« {name} » lancé (raccourci)." : $"« {name} » n'a pas pu être lancé.");
                    break;
            }
        }
        catch (Exception ex)
        {
            ActionRan?.Invoke($"Raccourci : {ex.Message}");
        }
    }
}
