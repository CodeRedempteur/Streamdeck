using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using StreamDeck.Models;
using StreamDeck.ScriptBuilder.Models;
using StreamDeck.ScriptBuilder.Services;

namespace StreamDeck.ScriptBuilder.Generation;

/// <summary>Transforme un projet en script PowerShell (et relit un projet depuis un script créé par l'application).</summary>
public static partial class ScriptGenerator
{
    /// <summary>Préfixe des lignes qui contiennent le projet (JSON en base64) : ce sont des commentaires PowerShell.</summary>
    public const string ProjectMarker = "#SDB:v1:";

    private const string NewLine = "\r\n";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    // ---------------------------------------------------------------------------------------------
    //  Génération
    // ---------------------------------------------------------------------------------------------

    /// <param name="phones">
    /// v1.34.13, plusieurs possibles depuis la v1.34.21 (« plusieurs téléphones ») : téléphones configurés
    /// (<see cref="BuilderConfig.PhoneTargets"/>). Chaque étape « Téléphone » (voir <see cref="StepKind.Phone"/>)
    /// choisit le sien par <see cref="ScriptStep.PhoneId"/> (ou <paramref name="defaultPhoneId"/> si null/supprimé) —
    /// embarqué telle quelle (adresse IP/port/jeton en dur) dans le script généré, comme le reste des réglages d'une
    /// étape. Conséquence : changer un téléphone dans Réglages ne prend effet qu'aux prochains scripts ENREGISTRÉS
    /// (pas déjà écrits sur le disque). Aucune correspondance trouvée (liste vide, ou id inconnu et pas de défaut) :
    /// l'étape échoue clairement à l'exécution plutôt que d'utiliser une adresse vide silencieusement.
    /// </param>
    /// <param name="defaultPhoneId">v1.34.21 : <see cref="BuilderConfig.DefaultPhoneId"/>, utilisé par une étape dont le <see cref="ScriptStep.PhoneId"/> est null ou ne correspond plus à aucun téléphone de <paramref name="phones"/>.</param>
    public static string Generate(ScriptProject project, IReadOnlyList<PhoneTarget>? phones = null, Guid? defaultPhoneId = null)
    {
        var phoneList = phones ?? Array.Empty<PhoneTarget>();
        PhoneTarget? ResolvePhone(Guid? id) =>
            (id is { } pid ? phoneList.FirstOrDefault(p => p.Id == pid) : null)
            ?? (defaultPhoneId is { } did ? phoneList.FirstOrDefault(p => p.Id == did) : null)
            ?? phoneList.FirstOrDefault();

        var lines = new List<string>();
        var steps = project.Steps;
        var active = Flatten(steps).Where(s => s.Enabled).ToList();
        var varNames = new HashSet<string>(project.Variables.Select(v => v.Name).Where(n => !string.IsNullOrWhiteSpace(n)), StringComparer.Ordinal);

        lines.Add("# =========================================================");
        lines.Add("#  " + Comment(string.IsNullOrWhiteSpace(project.Name) ? "Script" : project.Name));
        if (!string.IsNullOrWhiteSpace(project.Description))
            foreach (var d in project.Description.Replace("\r", "").Split('\n'))
                lines.Add("#  " + Comment(d));
        lines.Add("#  Créé avec Stream Deck Script Builder : rouvre-le dans l'application pour le modifier.");
        lines.Add($"#  Appelé quand une requête arrive sur /{Comment(project.Route)}?token=...");
        lines.Add("# =========================================================");

        if (project.Variables.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("# Variables envoyées par le Stream Deck ({nom} dans le texte des étapes ci-dessous) : réglées");
            lines.Add("# sur le bouton (Réglée ici / Au lancement), transmises par le serveur sous ces mêmes noms.");
            lines.Add("param(");
            for (var i = 0; i < project.Variables.Count; i++)
            {
                var v = project.Variables[i];
                if (string.IsNullOrWhiteSpace(v.Name)) continue;
                var comma = i < project.Variables.Count - 1 ? "," : "";
                lines.Add(v.Kind == VariableKind.Number
                    ? $"    [int]${v.Name} = {v.Min}{comma}"
                    : $"    [string]${v.Name} = \"\"{comma}");
            }
            lines.Add(")");
        }

        lines.Add("$NFCServerDir = Split-Path -Parent $PSScriptRoot");
        lines.Add($". (Join-Path $NFCServerDir \"{BuilderHelpers.FileName}\")");
        if (active.Any(s => s.Kind is StepKind.Volume or StepKind.Media))
            lines.Add(". (Join-Path $NFCServerDir \"NfcNative.ps1\")");
        if (active.Any(s => s.Kind == StepKind.Spotify))
            lines.Add(". (Join-Path $NFCServerDir \"SpotifyHelper.ps1\")");
        // Send-PhoneCommand vit dans BuilderUtils.ps1 (toujours dot-sourcé ci-dessus, voir BuilderHelpers.cs) : pas
        // besoin d'un fichier séparé ici, contrairement à Spotify/Volume.

        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var tag = (i + 1).ToString();

            lines.Add(string.Empty);
            lines.Add($"# ---- {tag}. {Comment(Title(step))}{(step.Enabled ? "" : " (désactivée)")} ----");

            var body = StepLines(step, tag, varNames, ResolvePhone);
            if (step.Enabled)
                lines.AddRange(body);
            else
                lines.AddRange(body.Select(l => "# " + l));
        }

        lines.Add(string.Empty);
        lines.Add("# ---- Projet du constructeur (ne pas modifier ces lignes) ----");
        lines.AddRange(EncodeProject(project));

        return string.Join(NewLine, lines) + NewLine;
    }

    /// <summary>Toutes les étapes, y compris celles des branches d'un Basculer (récursif).</summary>
    private static IEnumerable<ScriptStep> Flatten(IEnumerable<ScriptStep> steps)
    {
        foreach (var step in steps)
        {
            yield return step;
            foreach (var sub in Flatten(step.OnSteps)) yield return sub;
            foreach (var sub in Flatten(step.OffSteps)) yield return sub;
        }
    }

    /// <summary>Texte court décrivant une étape (titres du script et de l'interface).</summary>
    public static string Title(ScriptStep s) => s.Kind switch
    {
        StepKind.OpenApp => "Ouvrir " + Label(s.Path, "un programme"),
        StepKind.OpenBrowser => "Ouvrir le site " + Label(s.Url, "web"),
        StepKind.Window => "Fenêtre de " + Label(s.ProcessName, "?"),
        StepKind.Wait => $"Attendre {s.Seconds} s",
        StepKind.Close => s.CloseTarget == CloseTarget.BrowserWindowTitle
            ? "Fermer la page " + Label(s.WindowTitleContains, "?")
            : "Fermer " + Label(s.ProcessName, "un programme"),
        StepKind.Volume => s.VolumeMode switch
        {
            VolumeMode.Set => $"Volume à {s.Level} %",
            VolumeMode.Mute => "Couper le son",
            VolumeMode.Unmute => "Remettre le son",
            _ => "Basculer le muet"
        },
        StepKind.Media => "Touche média : " + s.MediaKey,
        StepKind.Spotify => s.SpotifyMode == SpotifyMode.Playlist ? "Spotify : playlist" : "Spotify : titres likés",
        StepKind.SendKeys => "Envoyer des touches",
        StepKind.Toggle => "Basculer" + (s.OnSteps.Count + s.OffSteps.Count > 0 ? $" ({s.OnSteps.Count} / {s.OffSteps.Count} actions)" : ""),
        StepKind.Macro => s.MacroActions.Count == 0 ? "Macro (rien d'enregistré)" : $"Macro ({s.MacroActions.Count} évènement{(s.MacroActions.Count > 1 ? "s" : "")})",
        StepKind.Phone => s.PhoneAction == PhoneAction.FindPhone ? "Téléphone : sonnerie/alarme" : "Téléphone : ouvrir " + Label(s.PhoneAppPackage, "une application"),
        _ => "Code PowerShell"
    };

    /// <summary>Nom de processus à surveiller : celui saisi, sinon le nom du programme lancé.</summary>
    public static string ProcessNameFor(ScriptStep s)
    {
        if (!string.IsNullOrWhiteSpace(s.Window.ProcessName)) return s.Window.ProcessName.Trim();
        if (s.Target != TargetKind.Program || string.IsNullOrWhiteSpace(s.Path)) return string.Empty;

        // .lnk : le nom du raccourci n'est pas celui du processus, on ne devine pas.
        if (s.Path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) return string.Empty;

        var file = s.Path.Replace('/', '\\').TrimEnd('\\');
        file = file[(file.LastIndexOf('\\') + 1)..];
        var dot = file.LastIndexOf('.');
        return dot > 0 ? file[..dot] : file;
    }

    private static IEnumerable<string> StepLines(ScriptStep s, string tag, ISet<string> varNames, Func<Guid?, PhoneTarget?> resolvePhone)
    {
        switch (s.Kind)
        {
            case StepKind.OpenApp:
                return OpenApp(s, tag, varNames);

            case StepKind.OpenBrowser:
                return new[]
                {
                    $"Open-BrowserPage -Url {QVar(s.Url, varNames)} -Browser {Q(string.IsNullOrWhiteSpace(s.Browser) ? "chrome" : s.Browser)} " +
                    $"-Screen {s.Window.Screen} -State {s.Window.State} | Out-Null"
                };

            case StepKind.Window:
            {
                var name = s.ProcessName.Trim();
                if (name.Length == 0) return new[] { "# (nom de processus manquant : étape ignorée)" };
                return new[]
                {
                    $"$window{tag} = Find-ProcessWindow -ProcessName {Q(name)} -TimeoutSeconds {Positive(s.Window.WaitSeconds, 30)} -AnyWindow",
                    ManageLine($"$window{tag}", s.Window)
                };
            }

            case StepKind.Wait:
                return new[] { $"Start-Sleep -Seconds {Math.Max(0, s.Seconds)}" };

            case StepKind.Close when s.CloseTarget == CloseTarget.BrowserWindowTitle:
                return s.WindowTitleContains.Trim().Length == 0
                    ? new[] { "# (titre à rechercher manquant : étape ignorée)" }
                    : new[] { $"Close-BrowserWindow -Browser {Q(string.IsNullOrWhiteSpace(s.Browser) ? "chrome" : s.Browser)} -TitleContains {Q(s.WindowTitleContains.Trim())}{(s.Force ? " -Force" : "")}" };

            case StepKind.Close:
                return s.ProcessName.Trim().Length == 0
                    ? new[] { "# (nom de processus manquant : étape ignorée)" }
                    : new[] { $"Stop-ProcessByName -Name {Q(s.ProcessName.Trim())}{(s.Force ? " -Force" : "")}" };

            case StepKind.Volume:
                return new[]
                {
                    s.VolumeMode switch
                    {
                        VolumeMode.Set => $"Set-NfcVolume -Level {Math.Clamp(s.Level, 0, 100)}",
                        VolumeMode.Mute => "Set-NfcMute -Muted $true",
                        VolumeMode.Unmute => "Set-NfcMute -Muted $false",
                        _ => "Set-NfcMute -Muted (-not (Get-NfcAudio).muted)"
                    }
                };

            case StepKind.Media:
            {
                var key = s.MediaKey is "playpause" or "next" or "prev" or "stop" ? s.MediaKey : "playpause";
                return new[] { $"Send-NfcMediaKey -Key {Q(key)}" };
            }

            case StepKind.Spotify:
                return Spotify(s);

            case StepKind.SendKeys:
                return new[]
                {
                    "$wshell = New-Object -ComObject WScript.Shell",
                    $"$wshell.SendKeys({Q(s.Keys)})"
                };

            case StepKind.Toggle:
                return ToggleLines(s, tag, varNames, resolvePhone);

            case StepKind.Macro:
                return MacroLines(s, tag);

            case StepKind.Phone:
                return Phone(s, resolvePhone(s.PhoneId));

            default:
                return (s.Code ?? string.Empty).Replace("\r", "").Split('\n').Select(l => l.TrimEnd());
        }
    }

    private static IEnumerable<string> OpenApp(ScriptStep s, string tag, ISet<string> varNames)
    {
        var lines = new List<string>();
        var name = ProcessNameFor(s);
        var manage = s.Window.Enabled && name.Length > 0;

        var start = "Start-Process -FilePath " + Q(s.Path);
        if (s.Target == TargetKind.Program)
        {
            if (!string.IsNullOrWhiteSpace(s.Arguments)) start += " -ArgumentList " + QVar(s.Arguments, varNames);

            // Beaucoup de programmes (dont OBS) ont besoin que leur dossier de travail soit le leur pour retrouver
            // leurs propres fichiers (locale, ressources…) : par défaut, celui du programme lui-même plutôt que
            // celui du serveur (qui serait utilisé si on ne précisait rien à Start-Process).
            var workDir = !string.IsNullOrWhiteSpace(s.WorkingDirectory) ? s.WorkingDirectory : DirectoryOf(s.Path);
            if (!string.IsNullOrWhiteSpace(workDir)) start += " -WorkingDirectory " + Q(workDir);

            if (s.RunAsAdmin) start += " -Verb RunAs";
        }

        if (manage) lines.Add($"$start{tag} = Get-Date");

        if (s.SkipIfRunning && name.Length > 0)
        {
            lines.Add($"if (Get-Process -Name {Q(name)} -ErrorAction SilentlyContinue) {{");
            lines.Add($"    Write-Host {Q(name + " est déjà lancé")}");
            lines.Add("} else {");
            lines.Add("    " + start);
            lines.Add("}");
        }
        else
        {
            lines.Add(start);
        }

        if (manage)
        {
            lines.Add($"$window{tag} = Find-ProcessWindow -ProcessName {Q(name)} -TimeoutSeconds {Positive(s.Window.WaitSeconds, 30)} -StartedAfter $start{tag}");
            lines.Add(ManageLine($"$window{tag}", s.Window));
        }
        else if (s.Window.Enabled)
        {
            lines.Add("# (nom de processus manquant : la fenêtre n'est pas gérée)");
        }

        return lines;
    }

    private static string ManageLine(string variable, WindowOptions w)
    {
        var line = $"Set-ManagedWindow -Process {variable} -Screen {w.Screen} -State {w.State}";
        return w.Foreground ? line + " -Foreground" : line;
    }

    /// <summary>
    /// Deux suites d'actions, avec un état mémorisé entre deux exécutions séparées du script (un fichier-témoin, puisque
    /// chaque appui sur le bouton relance un tout nouveau processus PowerShell : rien ne peut survivre en mémoire).
    /// </summary>
    private static IEnumerable<string> ToggleLines(ScriptStep s, string tag, ISet<string> varNames, Func<Guid?, PhoneTarget?> resolvePhone)
    {
        var file = $"$toggleFile{tag}";
        var lines = new List<string>
        {
            $"{file} = Join-Path $NFCServerDir \".toggle-state\\{s.Id:N}.flag\"",
            $"if (-not (Test-Path -LiteralPath {file})) {{",
            $"    New-Item -ItemType Directory -Force -Path (Split-Path -Parent {file}) | Out-Null",
            $"    New-Item -ItemType File -Force -Path {file} | Out-Null"
        };
        lines.AddRange(BranchLines(s.OnSteps, tag + "a", "    ", varNames, resolvePhone));
        lines.Add("} else {");
        lines.Add($"    Remove-Item -LiteralPath {file} -Force");
        lines.AddRange(BranchLines(s.OffSteps, tag + "b", "    ", varNames, resolvePhone));
        lines.Add("}");
        return lines;
    }

    /// <summary>Lignes (indentées) d'une des deux branches d'un Basculer : mêmes règles que les étapes de premier niveau.</summary>
    private static IEnumerable<string> BranchLines(List<ScriptStep> subSteps, string branchTag, string indent, ISet<string> varNames, Func<Guid?, PhoneTarget?> resolvePhone)
    {
        if (subSteps.Count == 0)
        {
            yield return indent + "# (aucune action)";
            yield break;
        }

        for (var i = 0; i < subSteps.Count; i++)
        {
            var sub = subSteps[i];
            var subTag = branchTag + (i + 1);

            yield return indent + $"#   -> {i + 1}. {Comment(Title(sub))}{(sub.Enabled ? "" : " (désactivée)")}";

            foreach (var line in StepLines(sub, subTag, varNames, resolvePhone))
                yield return indent + (sub.Enabled ? line : "# " + line);
        }
    }

    /// <summary>
    /// Rejoue les évènements enregistrés (souris + clavier) via <c>user32.dll</c> (mêmes fonctions natives que
    /// n'importe quel logiciel de macro Windows). La touche d'annulation est vérifiée par tranches de 20 ms pendant
    /// chaque pause, pour réagir presque tout de suite plutôt qu'à la fin du délai en cours.
    /// </summary>
    /// <summary>
    /// Nom du fichier (dans le dossier du serveur, à côté de server.ps1) dont l'HEURE DE MODIFICATION sert de signal
    /// « arrêter toutes les macros en cours » — écrit par l'application (raccourci clavier ou bouton) quand
    /// l'utilisateur veut stopper une macro tout de suite, quelle que soit sa touche d'annulation configurée. Chaque
    /// relecture note l'heure à laquelle ELLE a démarré, et n'obéit qu'à un signal plus récent (un signal ancien,
    /// laissé par un appui précédent, n'arrête pas une macro qui démarre après coup).
    /// </summary>
    public const string MacroStopSignalFileName = "MacroStop.signal";

    private static IEnumerable<string> MacroLines(ScriptStep s, string tag)
    {
        if (s.MacroActions.Count == 0)
            return new[] { "# (aucune action enregistrée : étape ignorée — ouvre-la dans l'application pour enregistrer une macro)" };

        var stopOnMove = s.MacroStopOnManualMove;
        var actionsVar = $"$sdMacroActions{tag}";
        var waitFn = $"Wait-SdMacroDelay{tag}";
        var stopFileVar = $"$sdMacroStopFile{tag}";
        var startedVar = $"$sdMacroStarted{tag}";

        // SdPoint/GetCursorPos sont TOUJOURS déclarés ici (même si cette macro-ci n'en a pas besoin) : le garde-fou
        // ci-dessous (« si le type SdMacro n'existe pas encore ») ne s'exécute qu'une fois par script généré — avec
        // plusieurs étapes Macro, seule la première rencontrée définit réellement le type. Les rendre conditionnels à
        // l'option de CETTE étape aurait pu laisser GetCursorPos absent si une étape sans l'option passe en premier.
        var lines = new List<string>
        {
            "if (-not ('SdMacro' -as [type])) {",
            "    Add-Type @'",
            "using System;",
            "using System.Runtime.InteropServices;",
            "public struct SdPoint { public int X; public int Y; }",
            "public class SdMacro {",
            "    [DllImport(\"user32.dll\")] public static extern bool SetCursorPos(int x, int y);",
            "    [DllImport(\"user32.dll\")] public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);",
            "    [DllImport(\"user32.dll\")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);",
            "    [DllImport(\"user32.dll\")] public static extern short GetAsyncKeyState(int vKey);",
            "    [DllImport(\"user32.dll\")] public static extern bool GetCursorPos(out SdPoint lpPoint);",
            "}",
            "'@",
            "}",
            string.Empty
        };

        // La touche d'annulation est vérifiée par tranches de 20 ms pendant chaque pause, pour réagir presque tout de
        // suite plutôt qu'à la fin du délai en cours. De même pour le signal « arrêter toutes les macros » (fichier
        // partagé, voir MacroStopSignalFileName) : présent quel que soit le réglage de cette macro, pour qu'un raccourci
        // clavier de l'application puisse stopper N'IMPORTE QUELLE macro en cours, même celle-ci. Avec l'option « arrêt
        // si la souris bouge à la main » : la même boucle compare aussi la position réelle de la souris à celle que la
        // macro vient elle-même de placer ($WatchX/$WatchY, sentinelle -999999 tant qu'aucune action souris n'a encore
        // eu lieu) — un écart de plus de quelques pixels veut dire que la main a repris la souris, donc on arrête aussi.
        lines.Add($"function {waitFn} {{");
        lines.Add(stopOnMove
            ? "    param([int]$Ms, [int]$AbortVk, [string]$StopFile, [datetime]$StartedUtc, [int]$WatchX = -999999, [int]$WatchY = -999999)"
            : "    param([int]$Ms, [int]$AbortVk, [string]$StopFile, [datetime]$StartedUtc)");
        lines.Add("    function Test-SdMacroStopSignal {");
        lines.Add("        if (-not (Test-Path -LiteralPath $StopFile -PathType Leaf)) { return $false }");
        lines.Add("        $sig = (Get-Item -LiteralPath $StopFile -ErrorAction SilentlyContinue).LastWriteTimeUtc");
        lines.Add("        return ($sig) -and ($sig -gt $StartedUtc)");
        lines.Add("    }");
        if (stopOnMove)
        {
            lines.Add("    function Test-SdMouseMoved {");
            lines.Add("        if ($WatchX -eq -999999) { return $false }");
            lines.Add("        $pos = New-Object SdPoint");
            lines.Add("        if (-not [SdMacro]::GetCursorPos([ref]$pos)) { return $false }");
            lines.Add("        return ([Math]::Abs($pos.X - $WatchX) -gt 3) -or ([Math]::Abs($pos.Y - $WatchY) -gt 3)");
            lines.Add("    }");
        }
        lines.Add("    $remaining = $Ms");
        lines.Add("    while ($remaining -gt 0) {");
        lines.Add(stopOnMove
            ? "        if ((([SdMacro]::GetAsyncKeyState($AbortVk)) -band 0x8000) -or (Test-SdMacroStopSignal) -or (Test-SdMouseMoved)) { return $true }"
            : "        if ((([SdMacro]::GetAsyncKeyState($AbortVk)) -band 0x8000) -or (Test-SdMacroStopSignal)) { return $true }");
        lines.Add("        $chunk = [Math]::Min(20, $remaining)");
        lines.Add("        Start-Sleep -Milliseconds $chunk");
        lines.Add("        $remaining -= $chunk");
        lines.Add("    }");
        lines.Add(stopOnMove
            ? "    return ((([SdMacro]::GetAsyncKeyState($AbortVk)) -band 0x8000) -ne 0) -or (Test-SdMacroStopSignal) -or (Test-SdMouseMoved)"
            : "    return ((([SdMacro]::GetAsyncKeyState($AbortVk)) -band 0x8000) -ne 0) -or (Test-SdMacroStopSignal)");
        lines.Add("}");
        lines.Add(string.Empty);
        lines.Add($"{actionsVar} = @(");

        foreach (var a in s.MacroActions)
            lines.Add($"    @{{ Delay = {Math.Max(0, a.DelayMs)}; Type = '{a.Type}'; Key = {a.Key}; X = {a.X}; Y = {a.Y}; Button = {Math.Clamp(a.Button, 0, 2)}; Wheel = {a.Wheel} }}");

        lines.Add(")");
        lines.Add(string.Empty);
        lines.Add($"{stopFileVar} = Join-Path $NFCServerDir {Q(MacroStopSignalFileName)}");
        lines.Add($"{startedVar} = (Get-Date).ToUniversalTime()");
        if (stopOnMove) lines.Add("$__watchX = -999999; $__watchY = -999999");
        lines.Add($"foreach ($__a in {actionsVar}) {{");
        lines.Add(stopOnMove
            ? $"    if ({waitFn} -Ms $__a.Delay -AbortVk {AbortVk(s.MacroAbortKey)} -StopFile {stopFileVar} -StartedUtc {startedVar} -WatchX $__watchX -WatchY $__watchY) {{ Write-Host \"Macro arrêtée (touche « {ControlKeyLabel(s.MacroAbortKey)} », souris déplacée à la main, ou arrêt global demandé)\"; break }}"
            : $"    if ({waitFn} -Ms $__a.Delay -AbortVk {AbortVk(s.MacroAbortKey)} -StopFile {stopFileVar} -StartedUtc {startedVar}) {{ Write-Host \"Macro arrêtée (touche « {ControlKeyLabel(s.MacroAbortKey)} » ou arrêt global demandé)\"; break }}");
        lines.Add("    switch ($__a.Type) {");
        lines.Add("        'KeyDown'    { [SdMacro]::keybd_event([byte]$__a.Key, 0, 0, [UIntPtr]::Zero) }");
        lines.Add("        'KeyUp'      { [SdMacro]::keybd_event([byte]$__a.Key, 0, 2, [UIntPtr]::Zero) }");
        lines.Add(stopOnMove
            ? "        'MouseMove'  { [SdMacro]::SetCursorPos($__a.X, $__a.Y); $__watchX = $__a.X; $__watchY = $__a.Y }"
            : "        'MouseMove'  { [SdMacro]::SetCursorPos($__a.X, $__a.Y) }");
        lines.Add(stopOnMove
            ? "        'MouseDown'  { [SdMacro]::SetCursorPos($__a.X, $__a.Y); [SdMacro]::mouse_event((@(0x0002,0x0008,0x0020))[$__a.Button], 0, 0, 0, [UIntPtr]::Zero); $__watchX = $__a.X; $__watchY = $__a.Y }"
            : "        'MouseDown'  { [SdMacro]::SetCursorPos($__a.X, $__a.Y); [SdMacro]::mouse_event((@(0x0002,0x0008,0x0020))[$__a.Button], 0, 0, 0, [UIntPtr]::Zero) }");
        lines.Add(stopOnMove
            ? "        'MouseUp'    { [SdMacro]::SetCursorPos($__a.X, $__a.Y); [SdMacro]::mouse_event((@(0x0004,0x0010,0x0040))[$__a.Button], 0, 0, 0, [UIntPtr]::Zero); $__watchX = $__a.X; $__watchY = $__a.Y }"
            : "        'MouseUp'    { [SdMacro]::SetCursorPos($__a.X, $__a.Y); [SdMacro]::mouse_event((@(0x0004,0x0010,0x0040))[$__a.Button], 0, 0, 0, [UIntPtr]::Zero) }");
        lines.Add("        'MouseWheel' { [SdMacro]::mouse_event(0x0800, 0, 0, [uint32]$__a.Wheel, [UIntPtr]::Zero) }");
        lines.Add("    }");
        lines.Add("}");

        return lines;
    }

    /// <summary>Code de touche Windows (VK) d'une <see cref="MacroControlKey"/>, littéral entier dans le script généré.</summary>
    private static int AbortVk(MacroControlKey key) => key switch
    {
        MacroControlKey.Escape => 0x1B,
        MacroControlKey.F9 => 0x78,
        MacroControlKey.F10 => 0x79,
        MacroControlKey.F11 => 0x7A,
        MacroControlKey.F12 => 0x7B,
        MacroControlKey.Pause => 0x13,
        MacroControlKey.ScrollLock => 0x91,
        _ => 0x1B
    };

    /// <summary>Nom convivial d'une <see cref="MacroControlKey"/> (message affiché quand la relecture est annulée).</summary>
    public static string ControlKeyLabel(MacroControlKey key) => key switch
    {
        MacroControlKey.Escape => "Échap",
        MacroControlKey.Pause => "Pause",
        MacroControlKey.ScrollLock => "Arrêt défil.",
        _ => key.ToString()
    };

    private static IEnumerable<string> Spotify(ScriptStep s)
    {
        var lines = new List<string>();

        if (s.LaunchSpotify)
        {
            lines.Add("Open-SpotifyMinimized");
            lines.Add("Start-Sleep -Seconds 5");
        }

        if (s.SpotifyMode == SpotifyMode.LikedSongs)
        {
            lines.Add("Start-SpotifyLikedSongs -SkipLaunch");
            return lines;
        }

        var call = "Start-SpotifyPlaylist -ContextUri " + Q(s.ContextUri);
        if (!string.IsNullOrWhiteSpace(s.DeviceName)) call += " -DeviceNameContains " + Q(s.DeviceName);
        if (!string.IsNullOrWhiteSpace(s.DeviceType)) call += " -DeviceTypeFallback " + Q(s.DeviceType);
        lines.Add(call);
        return lines;
    }

    private static IEnumerable<string> Phone(ScriptStep s, PhoneTarget? phone)
    {
        if (phone is null || string.IsNullOrWhiteSpace(phone.IpAddress))
            return new[] { "# (aucun téléphone configuré dans Réglages → Téléphone : étape ignorée)" };

        var call = "Send-PhoneCommand -IpAddress " + Q(phone.IpAddress.Trim()) +
                   " -Port " + phone.Port +
                   " -Token " + Q(phone.Token);

        if (s.PhoneAction == PhoneAction.OpenApp)
        {
            var package = s.PhoneAppPackage.Trim();
            if (package.Length == 0) return new[] { "# (nom de paquet de l'application manquant : étape ignorée)" };
            call += " -Action 'openapp' -AppPackage " + Q(package);
        }
        else
        {
            call += " -Action 'find'";
        }

        return new[] { call };
    }

    private static int Positive(int value, int fallback) => value > 0 ? value : fallback;

    /// <summary>
    /// Dossier contenant le fichier, d'après le texte du chemin (chemins Windows, « \ » ou « / »). Null si le chemin
    /// n'en a pas (ex : juste « notepad.exe », trouvé via PATH) — analysé en texte plutôt que via System.IO.Path, qui
    /// suit les règles du système d'exploitation courant et lirait mal un chemin Windows ailleurs que sur Windows.
    /// </summary>
    private static string? DirectoryOf(string path)
    {
        var p = path.Trim();
        var i = p.LastIndexOfAny(new[] { '\\', '/' });
        return i > 0 ? p[..i] : null;
    }

    private static string Label(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var v = value.Trim().Replace('/', '\\');
        var name = v.Contains('\\') ? v[(v.TrimEnd('\\').LastIndexOf('\\') + 1)..] : v;
        return name.Length == 0 ? v : name;
    }

    // ---------------------------------------------------------------------------------------------
    //  Échappement
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Chaîne PowerShell entre apostrophes. Dans ce type de chaîne seule l'apostrophe est spéciale, mais PowerShell
    /// considère aussi les apostrophes typographiques (‘ ’ ‚ ‛) comme des apostrophes : on double chacune d'elles.
    /// </summary>
    public static string Q(string? value)
    {
        var text = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ");
        var sb = new StringBuilder("'");
        foreach (var c in text)
        {
            sb.Append(c);
            if (c is '\'' or '\u2018' or '\u2019' or '\u201A' or '\u201B') sb.Append(c);
        }

        return sb.Append('\'').ToString();
    }

    private static readonly Regex VariablePattern = new(@"\{([a-zA-Z_][a-zA-Z0-9_]*)\}", RegexOptions.Compiled);

    /// <summary>
    /// Comme <see cref="Q"/>, mais remplace {nom} par $nom (interpolation PowerShell) pour chaque nom qui figure
    /// dans <paramref name="varNames"/> — le reste du texte (accolades qui ne correspondent à rien, guillemets,
    /// dollars, apostrophes) est échappé pour rester littéral. Aucune variable déclarée dans le texte : revient à
    /// <see cref="Q"/> telle quelle (chaîne entre apostrophes, jamais interpolée).
    /// </summary>
    public static string QVar(string? value, ISet<string> varNames)
    {
        var text = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ");
        if (varNames.Count == 0 || !VariablePattern.IsMatch(text)) return Q(text);

        var sb = new StringBuilder("\"");
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '{')
            {
                var end = text.IndexOf('}', i + 1);
                if (end > i)
                {
                    var name = text[(i + 1)..end];
                    if (varNames.Contains(name))
                    {
                        sb.Append('$').Append(name);
                        i = end + 1;
                        continue;
                    }
                }
            }

            if (c is '`' or '"' or '$') sb.Append('`');
            sb.Append(c);
            i++;
        }

        return sb.Append('"').ToString();
    }

    /// <summary>Tous les {nom} présents dans les champs de texte qui acceptent des variables (URL, arguments d'un
    /// programme), y compris dans les branches d'un Basculer — pour suggérer les variables à déclarer.</summary>
    public static IReadOnlyList<string> DiscoverVariableNames(ScriptProject project)
    {
        var names = new List<string>();
        foreach (var s in Flatten(project.Steps))
        {
            CollectVariableNames(s.Url, names);
            if (s.Target == TargetKind.Program) CollectVariableNames(s.Arguments, names);
        }
        return names.Distinct(StringComparer.Ordinal).ToList();
    }

    private static void CollectVariableNames(string? text, List<string> into)
    {
        if (string.IsNullOrEmpty(text)) return;
        foreach (Match m in VariablePattern.Matches(text)) into.Add(m.Groups[1].Value);
    }

    /// <summary>Texte sûr pour un commentaire sur une ligne.</summary>
    private static string Comment(string value) => value.Replace("\r", " ").Replace("\n", " ").Trim();

    // ---------------------------------------------------------------------------------------------
    //  Projet embarqué dans le script
    // ---------------------------------------------------------------------------------------------

    private static IEnumerable<string> EncodeProject(ScriptProject project)
    {
        var json = JsonSerializer.Serialize(project, JsonOptions);
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        for (var i = 0; i < base64.Length; i += 100)
            yield return ProjectMarker + base64.Substring(i, Math.Min(100, base64.Length - i));
    }

    /// <summary>Relit le projet d'un script créé par l'application. Renvoie null pour un script écrit à la main.</summary>
    public static ScriptProject? TryReadProject(string scriptText)
    {
        var chunks = scriptText.Replace("\r", "").Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.StartsWith(ProjectMarker, StringComparison.Ordinal))
            .Select(l => l[ProjectMarker.Length..])
            .ToList();

        if (chunks.Count == 0) return null;

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(string.Concat(chunks)));
            var project = JsonSerializer.Deserialize<ScriptProject>(json, JsonOptions);
            if (project is null) return null;

            project.Steps ??= new List<ScriptStep>();
            foreach (var step in project.Steps) FixUp(step);
            return project;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Comble les champs qui pourraient manquer (données plus anciennes) — récursif pour les branches d'un Basculer.</summary>
    private static void FixUp(ScriptStep step)
    {
        step.Window ??= new WindowOptions();
        step.OnSteps ??= new List<ScriptStep>();
        step.OffSteps ??= new List<ScriptStep>();
        foreach (var sub in step.OnSteps) FixUp(sub);
        foreach (var sub in step.OffSteps) FixUp(sub);
    }
}
