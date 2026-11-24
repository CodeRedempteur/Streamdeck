using System.Diagnostics;
using System.Text;

namespace StreamDeck.ScriptBuilder.Services;

public record TestResult(bool Started, int ExitCode, string Output, TimeSpan Duration, bool TimedOut);

/// <summary>Exécute un script tout de suite, comme le ferait le serveur (PowerShell caché, sortie récupérée).</summary>
public class ScriptTester(BuilderConfigService config)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    public async Task<TestResult> RunAsync(string scriptPath, CancellationToken ct = default)
    {
        var started = Stopwatch.StartNew();
        var output = new StringBuilder();
        var sync = new object();

        var info = new ProcessStartInfo
        {
            FileName = config.Current.PowerShellPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var arg in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scriptPath })
            info.ArgumentList.Add(arg);

        try
        {
            using var process = new Process { StartInfo = info };
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (sync) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (sync) output.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(Timeout);

            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch (Exception) { /* déjà terminé */ }
                var timedOut = !ct.IsCancellationRequested;
                lock (sync) output.AppendLine(timedOut ? "[arrêté : plus de 5 minutes]" : "[annulé]");
                return new TestResult(true, -1, output.ToString(), started.Elapsed, timedOut);
            }

            // Laisse les derniers événements de sortie arriver.
            process.WaitForExit();
            return new TestResult(true, process.ExitCode, output.ToString(), started.Elapsed, false);
        }
        catch (Exception ex)
        {
            return new TestResult(false, -1, $"Impossible de lancer « {config.Current.PowerShellPath} » : {ex.Message}", started.Elapsed, false);
        }
    }
}
