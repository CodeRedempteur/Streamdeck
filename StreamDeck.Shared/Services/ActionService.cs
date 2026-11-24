using StreamDeck.Models;

namespace StreamDeck.Services;

/// <summary>Exécute un bouton (script, réveil, action Windows ou enchaînement). Ne lève jamais d'exception.</summary>
public class ActionService(ConfigService config, ScriptRunner runner, WakeService wake, InfoDisplayService infoDisplay, FindPhoneService findPhone, TimerService timer)
{
    private static readonly TimeSpan DefaultWaitOnline = TimeSpan.FromSeconds(120);

    /// <param name="progress">Reçoit un texte à chaque étape d'un enchaînement.</param>
    /// <param name="prompted">Valeurs de variables saisies juste avant de lancer (celles en mode Prompt) ; les
    /// autres variables du bouton utilisent leur valeur réglée dans ses paramètres.</param>
    public async Task<RunResult> ExecuteAsync(Workspace workspace, DeckButton button, IProgress<string>? progress = null,
        IReadOnlyDictionary<string, string>? prompted = null, CancellationToken ct = default)
    {
        try
        {
            var pc = config.Current.PcFor(workspace, button);
            var variables = button.ResolveVariables(prompted);

            var result = button.Kind switch
            {
                ButtonKind.Script => await runner.RunScriptAsync(pc, button.Script, button.Label, false, variables, ct),
                ButtonKind.Wake => await WakeAsync(pc, button.Mac, ct),
                ButtonKind.PcAction => await RunCommandAsync(pc, button.Command, button.Label, ct),
                ButtonKind.Sequence => await RunSequenceAsync(pc, button, progress, ct),
                ButtonKind.Picker => new RunResult(false, "Ce bouton propose un choix : il doit être ouvert, pas exécuté directement."),
                ButtonKind.InfoDisplay => await ShowInfoDisplayAsync(pc, button, ct),
                ButtonKind.FindPhone => findPhone.Trigger(),
                ButtonKind.Timer => StartTimer(workspace, button, button.Timer),
                _ => new RunResult(false, "Ce bouton n'a pas d'action à lancer")
            };

            if (result.Ok && button.Kind == ButtonKind.Script) button.IncrementAutoVariables();

            // v1.36 : bouton Script (ou autre) avec un minuteur attaché — ex. "gâteau" : lance le script du four ET
            // un minuteur qui sonne à la fin. Lancé même si l'action principale a échoué le son resterait utile
            // (ex. le script a déjà démarré côté serveur malgré un message d'erreur de confirmation réseau).
            if (button.Kind != ButtonKind.Timer && button.AttachedTimer is { Phases.Count: > 0 })
                StartTimer(workspace, button, button.AttachedTimer);

            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new RunResult(false, "Annulé");
        }
        catch (Exception ex)
        {
            // Filet de sécurité : une exception ici bloquerait toute l'interface.
            return new RunResult(false, $"Erreur inattendue ({ex.GetType().Name})");
        }
    }

    /// <summary>Réveille un PC (adresse MAC du bouton, sinon celle du PC).</summary>
    public Task<RunResult> WakeAsync(PcProfile pc, string? macOverride = null, CancellationToken ct = default) =>
        wake.WakeAsync(string.IsNullOrWhiteSpace(macOverride) ? pc.Mac : macOverride, pc.ServerUrl, ct);

    /// <summary>Lance l'option choisie dans le popup d'un bouton de type Picker.</summary>
    /// <param name="prompted">Valeurs de variables saisies juste avant de lancer (celles en mode Prompt sur ce choix).</param>
    public async Task<RunResult> ExecuteOptionAsync(Workspace workspace, DeckButton owner, ChoiceOption option,
        IReadOnlyDictionary<string, string>? prompted = null, CancellationToken ct = default)
    {
        try
        {
            var pc = config.Current.PcFor(workspace, owner);

            var result = option.Type switch
            {
                StepType.Script => await runner.RunScriptAsync(pc, option.Script, option.Label, false, option.ResolveVariables(prompted), ct),
                StepType.PcAction => await RunCommandAsync(pc, option.Command, option.Label, ct),
                StepType.Wake => await WakeAsync(pc, owner.Mac, ct),
                _ => new RunResult(false, "Ce type de choix n'est pas pris en charge ici")
            };

            if (result.Ok && option.Type == StepType.Script) option.IncrementAutoVariables();
            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new RunResult(false, "Annulé");
        }
        catch (Exception ex)
        {
            return new RunResult(false, $"Erreur inattendue ({ex.GetType().Name})");
        }
    }

    /// <summary>Type InfoDisplay : prévient l'appli (si ciblée) via <see cref="InfoDisplayService"/>, et/ou demande
    /// au PC d'ouvrir sa propre fenêtre plein écran (si ciblé). Une image importée (data:...) ne peut être affichée
    /// que dans l'appli : trop volumineuse pour tenir dans l'URL envoyée au PC.</summary>
    private async Task<RunResult> ShowInfoDisplayAsync(PcProfile pc, DeckButton button, CancellationToken ct)
    {
        if (button.InfoKind == InfoKind.Image && string.IsNullOrWhiteSpace(button.InfoImageUrl))
            return new RunResult(false, "Aucune image définie pour cet affichage");

        var showApp = button.InfoTarget is InfoTarget.App or InfoTarget.Both;
        var showPc = button.InfoTarget is InfoTarget.Pc or InfoTarget.Both;

        if (showApp) infoDisplay.Show(button);

        if (!showPc)
            return new RunResult(true, $"{button.Label} : affiché");

        var isImportedImage = button.InfoKind == InfoKind.Image &&
                               button.InfoImageUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase);
        if (isImportedImage)
        {
            const string warn = "une image importée ne peut pas être envoyée au PC (utilise un lien https://… ou un chemin déjà présent sur le PC)";
            return showApp
                ? new RunResult(true, $"{button.Label} : affiché ici, mais pas sur le PC ({warn})", Warning: true)
                : new RunResult(false, char.ToUpperInvariant(warn[0]) + warn[1..]);
        }

        var pcResult = await runner.ShowInfoAsync(pc, button.InfoKind, button.InfoImageUrl, button.InfoDurationSeconds, ct);

        if (!showApp) return pcResult;
        return pcResult.Ok
            ? new RunResult(true, $"{button.Label} : affiché ici et sur le PC")
            : new RunResult(true, $"{button.Label} : affiché ici (PC : {pcResult.Message})", Warning: true);
    }

    /// <summary>Type Timer (et minuteur attaché à un autre type) : démarre le minuteur/chronomètre — affiché
    /// directement sur la touche d'origine (voir Home.razor/KeyTile, TimerService.OriginButtonId). v1.36.1,
    /// "si on clique dessus ça arrête" : un deuxième clic sur LA MÊME touche, pendant que son minuteur tourne
    /// (décompte, chrono, ou alarme Pomodoro en train de sonner), l'arrête plutôt que d'en redémarrer un autre.</summary>
    private RunResult StartTimer(Workspace workspace, DeckButton button, TimerConfig timerConfig)
    {
        if (timer.IsRunningFor(button.Id))
        {
            timer.Stop();
            return new RunResult(true, $"{button.Label} : minuteur arrêté");
        }

        if (timerConfig.Phases.Count == 0)
            return new RunResult(false, "Ce minuteur n'a aucune phase configurée");

        timer.Start(timerConfig, button.Label, workspace, button);
        return new RunResult(true, $"{button.Label} : minuteur démarré");
    }

    private async Task<RunResult> RunCommandAsync(PcProfile pc, string commandId, string label, CancellationToken ct)
    {
        var command = PcCommands.Find(commandId);
        return command is null
            ? new RunResult(false, "Aucune action Windows choisie pour ce bouton")
            : await runner.RunCommandAsync(pc, command, label, ct);
    }

    private async Task<RunResult> RunSequenceAsync(PcProfile pc, DeckButton button, IProgress<string>? progress, CancellationToken ct)
    {
        var steps = button.Steps;
        if (steps.Count == 0)
            return new RunResult(false, "Cet enchaînement n'a aucune étape");

        var warnings = 0;

        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            progress?.Report($"{button.Label} : étape {i + 1}/{steps.Count} ({Describe(step)})");

            var result = await RunStepAsync(pc, button.Mac, step, ct);
            if (!result.Ok)
                return new RunResult(false, $"Étape {i + 1}/{steps.Count} ({Describe(step)}) : {result.Message}");
            if (result.Warning) warnings++;
        }

        return new RunResult(true, $"{button.Label} : {steps.Count} étape(s) terminée(s)", warnings > 0);
    }

    private async Task<RunResult> RunStepAsync(PcProfile pc, string macOverride, SequenceStep step, CancellationToken ct)
    {
        switch (step.Type)
        {
            case StepType.Script:
                return await runner.RunScriptAsync(pc, step.Script, step.Script, step.WaitForEnd, null, ct);

            case StepType.PcAction:
                return await RunCommandAsync(pc, step.Command, step.Command, ct);

            case StepType.Wake:
                return await WakeAsync(pc, macOverride, ct);

            case StepType.WaitOnline:
                return await WaitOnlineAsync(pc, step.Seconds > 0 ? TimeSpan.FromSeconds(step.Seconds) : DefaultWaitOnline, ct);

            case StepType.Delay:
                await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(step.Seconds, 0, 3600)), ct);
                return new RunResult(true, "Pause terminée");

            default:
                return new RunResult(false, "Type d'étape inconnu");
        }
    }

    /// <summary>Attend que le serveur du PC réponde (par exemple après un réveil).</summary>
    private async Task<RunResult> WaitOnlineAsync(PcProfile pc, TimeSpan max, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + max;

        while (true)
        {
            var ping = await runner.PingAsync(pc.ServerUrl, ct);
            if (ping.Ok) return new RunResult(true, "PC en ligne");

            if (DateTime.UtcNow >= deadline)
                return new RunResult(false, $"PC toujours injoignable après {(int)max.TotalSeconds} s");

            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }
    }

    /// <summary>Texte court décrivant une étape (affiché pendant l'exécution et dans les erreurs).</summary>
    public static string Describe(SequenceStep step) => step.Type switch
    {
        StepType.Script => string.IsNullOrWhiteSpace(step.Script) ? "script" : $"script {step.Script}",
        StepType.PcAction => PcCommands.Find(step.Command)?.Label ?? "action Windows",
        StepType.Wake => "réveil du PC",
        StepType.WaitOnline => "attente du PC",
        StepType.Delay => $"pause {step.Seconds} s",
        _ => "étape"
    };
}
