using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using StreamDeck.ScriptBuilder.Generation;

namespace StreamDeck.ScriptBuilder.Services;

/// <param name="NeedsAdmin">Le serveur tourne en administrateur : il faut redémarrer avec les droits administrateur.</param>
public record RestartResult(bool Ok, string Message, IReadOnlyList<string> Details, bool NeedsAdmin = false, bool Cancelled = false);

/// <summary>État du serveur, pour un affichage rapide (widget de bureau) sans relancer tout un cycle arrêt/relance.</summary>
public record ServerStatus(bool Running, int? Pid);

/// <summary>Arrête le serveur NFC (server.ps1) puis le relance de la même façon.</summary>
public class ServerRestarter(BuilderConfigService config)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private sealed class ScriptOutput
    {
        public bool Ok { get; set; }
        public bool NeedsAdmin { get; set; }
        public string Message { get; set; } = "";
        public List<string> Details { get; set; } = new();
    }

    /// <summary>État actuel du serveur (pour le widget de bureau notamment) : tourne ou pas, et son PID.</summary>
    public async Task<ServerStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var folder = config.Current.ServerFolder;
        var server = Path.Combine(folder, "server.ps1");
        if (!File.Exists(server)) return new ServerStatus(false, null);

        var id = Guid.NewGuid().ToString("N");
        var resultPath = Path.Combine(Path.GetTempPath(), $"sdb-status-{id}.json");
        try
        {
            var script = StatusTemplate.Replace("__SERVER__", ScriptGenerator.Q(server)).Replace("__RESULT__", ScriptGenerator.Q(resultPath));
            var scriptPath = Path.Combine(Path.GetTempPath(), $"sdb-status-{id}.ps1");
            try
            {
                File.WriteAllText(scriptPath, script, new UTF8Encoding(true));
                using var process = StartHidden(config.Current.PowerShellPath, scriptPath, new StringBuilder());
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(15));
                try { await process.WaitForExitAsync(timeout.Token); } catch (OperationCanceledException) { return new ServerStatus(false, null); }

                if (!File.Exists(resultPath)) return new ServerStatus(false, null);
                var parsed = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(resultPath, Encoding.UTF8));
                var running = parsed.TryGetProperty("running", out var r) && r.GetBoolean();
                var pid = parsed.TryGetProperty("pid", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : (int?)null;
                return new ServerStatus(running, pid);
            }
            finally { TryDelete(scriptPath); }
        }
        catch (Exception)
        {
            return new ServerStatus(false, null);
        }
        finally
        {
            TryDelete(resultPath);
        }
    }

    /// <summary>Arrête le serveur sans le relancer (widget de bureau : bouton marche/arrêt).</summary>
    public Task<RestartResult> StopAsync(CancellationToken ct = default) => ToggleAsync(startAfter: false, ct);

    /// <summary>Démarre le serveur (caché) s'il ne tourne pas déjà.</summary>
    public Task<RestartResult> StartAsync(CancellationToken ct = default) => ToggleAsync(startAfter: true, ct);

    private async Task<RestartResult> ToggleAsync(bool startAfter, CancellationToken ct)
    {
        var folder = config.Current.ServerFolder;
        var server = Path.Combine(folder, "server.ps1");
        if (!File.Exists(server))
            return new RestartResult(false, "server.ps1 introuvable dans le dossier du serveur (Réglages).", Array.Empty<string>());

        // Réutilise exactement la même logique que le redémarrage (trouver, arrêter, relancer de la même façon) —
        // juste sans la seconde moitié quand on ne veut qu'arrêter.
        return startAfter
            ? await RunAsync(server, folder, config.Current.ServerRunAsAdmin, ct)
            : await RunStopOnlyAsync(server, ct);
    }

    private async Task<RestartResult> RunStopOnlyAsync(string server, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("N");
        var scriptPath = Path.Combine(Path.GetTempPath(), $"sdb-stop-{id}.ps1");
        var resultPath = Path.Combine(Path.GetTempPath(), $"sdb-stop-{id}.json");
        try
        {
            File.WriteAllText(scriptPath, StopTemplate.Replace("__SERVER__", ScriptGenerator.Q(server)).Replace("__RESULT__", ScriptGenerator.Q(resultPath)), new UTF8Encoding(true));
            using var process = StartHidden(config.Current.PowerShellPath, scriptPath, new StringBuilder());
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(Timeout);
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { return new RestartResult(false, "Annulé.", Array.Empty<string>()); }

            if (!File.Exists(resultPath)) return new RestartResult(false, "Pas de résultat.", Array.Empty<string>());
            var parsed = JsonSerializer.Deserialize<ScriptOutput>(File.ReadAllText(resultPath, Encoding.UTF8), Json) ?? new ScriptOutput { Message = "Résultat illisible." };
            return new RestartResult(parsed.Ok, parsed.Message, parsed.Details, parsed.NeedsAdmin);
        }
        finally
        {
            TryDelete(scriptPath);
            TryDelete(resultPath);
        }
    }

    private const string StatusTemplate = """"""
$ServerScript = __SERVER__
$ResultFile = __RESULT__
try {
    $all = @(Get-CimInstance Win32_Process -ErrorAction Stop | Where-Object { $_.CommandLine })
    $shells = @($all | Where-Object { $_.Name -match '^(powershell|pwsh)(\.exe)?$' })
    $found = @($shells | Where-Object { $_.CommandLine.IndexOf($ServerScript, [StringComparison]::OrdinalIgnoreCase) -ge 0 })
    if ($found.Count -eq 0) { $found = @($shells | Where-Object { $_.CommandLine -match '(?i)server\.ps1' }) }
    $out = if ($found.Count -gt 0) { @{ running = $true; pid = $found[0].ProcessId } } else { @{ running = $false; pid = $null } }
} catch {
    $out = @{ running = $false; pid = $null }
}
$out | ConvertTo-Json | Set-Content -Path $ResultFile -Encoding UTF8
"""""";

    private const string StopTemplate = """"""
$ErrorActionPreference = 'Continue'
$ServerScript = __SERVER__
$ResultFile = __RESULT__
$details = New-Object System.Collections.ArrayList
$ok = $true
$needsAdmin = $false
$message = 'Aucun serveur en cours d''execution.'
try {
    $all = @(Get-CimInstance Win32_Process -ErrorAction Stop | Where-Object { $_.ProcessId -ne $PID -and $_.CommandLine })
    $shells = @($all | Where-Object { $_.Name -match '^(powershell|pwsh)(\.exe)?$' })
    $found = @($shells | Where-Object { $_.CommandLine.IndexOf($ServerScript, [StringComparison]::OrdinalIgnoreCase) -ge 0 })
    if ($found.Count -eq 0) { $found = @($shells | Where-Object { $_.CommandLine -match '(?i)server\.ps1' }) }
    foreach ($p in $found) {
        try {
            Stop-Process -Id $p.ProcessId -Force -ErrorAction Stop
            [void]$details.Add("Arrete : $($p.Name) (PID $($p.ProcessId))")
            $message = 'Serveur arrete.'
        } catch {
            $ok = $false
            $needsAdmin = $true
            $message = "Impossible d'arreter le serveur (droits administrateur necessaires ?) : $($_.Exception.Message)"
        }
    }
} catch {
    $ok = $false
    $message = "Erreur : $($_.Exception.Message)"
}
@{ ok = $ok; needsAdmin = $needsAdmin; message = $message; details = @($details) } | ConvertTo-Json -Depth 4 | Set-Content -Path $ResultFile -Encoding UTF8
"""""";

    public async Task<RestartResult> RestartAsync(CancellationToken ct = default)
    {
        var folder = config.Current.ServerFolder;
        var server = Path.Combine(folder, "server.ps1");
        if (!File.Exists(server))
            return new RestartResult(false, "server.ps1 introuvable dans le dossier du serveur (Réglages).", Array.Empty<string>());

        var admin = config.Current.ServerRunAsAdmin;
        var result = await RunAsync(server, folder, admin, ct);

        // Le serveur actuel tourne en administrateur : on réessaie avec les droits (confirmation Windows).
        if (!result.Ok && result.NeedsAdmin && !admin)
        {
            var second = await RunAsync(server, folder, true, ct);
            var details = new List<string> { "Le serveur tourne en administrateur : redémarrage avec les droits administrateur." };
            details.AddRange(second.Details);
            return second with { Details = details };
        }

        return result;
    }

    private async Task<RestartResult> RunAsync(string server, string folder, bool elevated, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("N");
        var scriptPath = Path.Combine(Path.GetTempPath(), $"sdb-restart-{id}.ps1");
        var resultPath = Path.Combine(Path.GetTempPath(), $"sdb-restart-{id}.json");
        var shell = config.Current.PowerShellPath;

        try
        {
            File.WriteAllText(scriptPath, BuildScript(server, folder, resultPath, shell), new UTF8Encoding(true));

            var output = new StringBuilder();
            try
            {
                using var process = elevated ? StartElevated(shell, scriptPath) : StartHidden(shell, scriptPath, output);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(Timeout);

                try { await process.WaitForExitAsync(timeout.Token); }
                catch (OperationCanceledException)
                {
                    try { process.Kill(entireProcessTree: true); } catch (Exception) { /* déjà terminé */ }
                    return new RestartResult(false, ct.IsCancellationRequested ? "Annulé." : "Le redémarrage a pris trop de temps (plus d'une minute).", Array.Empty<string>());
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                return new RestartResult(false, "Confirmation administrateur refusée : le serveur n'a pas été redémarré.", Array.Empty<string>(), Cancelled: true);
            }
            catch (Exception ex)
            {
                return new RestartResult(false, $"Impossible de lancer « {shell} » : {ex.Message}", Array.Empty<string>());
            }

            if (!File.Exists(resultPath))
                return new RestartResult(false, "Le script de redémarrage n'a pas donné de résultat." + (output.Length > 0 ? " " + output.ToString().Trim() : ""), Array.Empty<string>());

            var parsed = JsonSerializer.Deserialize<ScriptOutput>(File.ReadAllText(resultPath, Encoding.UTF8), Json)
                         ?? new ScriptOutput { Message = "Résultat illisible." };
            return new RestartResult(parsed.Ok, parsed.Message, parsed.Details, parsed.NeedsAdmin);
        }
        finally
        {
            TryDelete(scriptPath);
            TryDelete(resultPath);
        }
    }

    private static Process StartHidden(string shell, string scriptPath, StringBuilder output)
    {
        var info = new ProcessStartInfo(shell)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var a in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scriptPath }) info.ArgumentList.Add(a);

        var process = new Process { StartInfo = info };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (output) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (output) output.AppendLine(e.Data); };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static Process StartElevated(string shell, string scriptPath) =>
        Process.Start(new ProcessStartInfo(shell)
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\""
        })!;

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (Exception) { /* fichier temporaire : sans gravité */ }
    }

    /// <summary>Script PowerShell qui trouve, arrête puis relance le serveur, et écrit son résultat en JSON.</summary>
    public static string BuildScript(string serverScript, string workDir, string resultFile, string shell) =>
        Template
            .Replace("__SERVER__", ScriptGenerator.Q(serverScript))
            .Replace("__WORKDIR__", ScriptGenerator.Q(workDir))
            .Replace("__RESULT__", ScriptGenerator.Q(resultFile))
            .Replace("__SHELL__", ScriptGenerator.Q(shell));

    private const string Template = """"""
$ErrorActionPreference = 'Continue'
$ServerScript = __SERVER__
$WorkDir = __WORKDIR__
$ResultFile = __RESULT__
$Shell = __SHELL__

$details = New-Object System.Collections.ArrayList
$ok = $false
$needsAdmin = $false
$message = ''

function Save-Result {
    $out = @{ ok = $ok; needsAdmin = $needsAdmin; message = $message; details = @($details) }
    $out | ConvertTo-Json -Depth 4 | Set-Content -Path $ResultFile -Encoding UTF8
}

try {
    $all = @(Get-CimInstance Win32_Process -ErrorAction Stop | Where-Object { $_.ProcessId -ne $PID -and $_.CommandLine })
    $shells = @($all | Where-Object { $_.Name -match '^(powershell|pwsh)(\.exe)?$' })

    # Le serveur : de preference le processus dont la ligne de commande contient le chemin complet de server.ps1.
    $found = @($shells | Where-Object { $_.CommandLine.IndexOf($ServerScript, [StringComparison]::OrdinalIgnoreCase) -ge 0 })
    if ($found.Count -eq 0) { $found = @($shells | Where-Object { $_.CommandLine -match '(?i)server\.ps1' }) }

    # Comment il a ete lance (pour le relancer pareil).
    $launchExe = $null
    $launchArgs = ''
    foreach ($p in $found) {
        if (-not $launchExe -and $p.ExecutablePath) {
            $launchExe = $p.ExecutablePath
            $cl = $p.CommandLine.Trim()
            if ($cl.StartsWith('"')) {
                $end = $cl.IndexOf('"', 1)
                if ($end -ge 0) { $launchArgs = $cl.Substring($end + 1).Trim() }
            } else {
                $sp = $cl.IndexOf(' ')
                if ($sp -ge 0) { $launchArgs = $cl.Substring($sp + 1).Trim() }
            }
        }
    }

    foreach ($p in $found) {
        try {
            Stop-Process -Id $p.ProcessId -Force -ErrorAction Stop
            [void]$details.Add("Arrete : $($p.Name) (PID $($p.ProcessId))")
        } catch {
            $needsAdmin = $true
            [void]$details.Add("Impossible d'arreter $($p.Name) (PID $($p.ProcessId)) : $($_.Exception.Message)")
        }
    }

    if ($needsAdmin) {
        $message = "Le serveur tourne avec des droits plus eleves que cette application."
    } else {
        $deadline = (Get-Date).AddSeconds(8)
        while ((Get-Date) -lt $deadline -and @($found | Where-Object { Get-Process -Id $_.ProcessId -ErrorAction SilentlyContinue }).Count -gt 0) {
            Start-Sleep -Milliseconds 250
        }
        Start-Sleep -Milliseconds 700

        if ($found.Count -eq 0 -or -not $launchExe) {
            [void]$details.Add('Aucun serveur en cours d''execution trouve : demarrage d''un nouveau serveur.')
            $launchExe = $Shell
            $launchArgs = '-NoProfile -ExecutionPolicy Bypass -File "' + $ServerScript + '"'
        }

        # -WindowStyle Hidden : le serveur ne doit jamais s'afficher comme une fenêtre/appli (console visible, alt-tab...) —
        # il tourne en tâche de fond du début à la fin, qu'il ait été démarré ici ou (voir StartServerHidden.vbs) au démarrage de Windows.
        $startArgs = @{ FilePath = $launchExe; WorkingDirectory = $WorkDir; PassThru = $true; WindowStyle = 'Hidden' }
        if ($launchArgs) { $startArgs.ArgumentList = $launchArgs }
        $proc = Start-Process @startArgs

        Start-Sleep -Seconds 3
        if ($proc.HasExited) {
            $message = "Le nouveau serveur s'est arrete aussitot (code $($proc.ExitCode)). Un autre serveur tourne peut-etre encore : ferme la console PowerShell ou il a ete lance, puis reessaie."
        } else {
            $ok = $true
            $message = "Serveur redemarre (PID $($proc.Id))."
        }
    }
} catch {
    $message = "Erreur : $($_.Exception.Message)"
}

Save-Result
"""""";
}
