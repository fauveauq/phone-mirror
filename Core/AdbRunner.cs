using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace PhoneMirror.Core;

public sealed class AdbException(string message) : Exception(message);

/// <summary>Lance des commandes adb, en ciblant éventuellement un appareil précis.</summary>
public sealed class AdbRunner(string adbPath, string? serial)
{
    /// <summary>Exécute une commande courte et renvoie sa sortie standard.</summary>
    public async Task<string> RunAsync(TimeSpan timeout, CancellationToken ct, params string[] args)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        using var process = new Process { StartInfo = CreateStartInfo(args) };
        Start(process);

        Task<string> stdout = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        Task<string> stderr = process.StandardError.ReadToEndAsync(timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            if (ct.IsCancellationRequested) throw;
            throw new AdbException($"« adb {Describe(args)} » n'a pas répondu en {timeout.TotalSeconds:0} s.");
        }

        string output = await stdout.ConfigureAwait(false);
        string error = await stderr.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            string detail = string.IsNullOrWhiteSpace(error) ? output : error;
            throw new AdbException($"« adb {Describe(args)} » a échoué : {detail.Trim()}");
        }

        return output;
    }

    /// <summary>Lance un processus adb de longue durée dont chaque ligne de sortie est transmise.</summary>
    public Process StartLongRunning(Action<string> onOutputLine, params string[] args)
    {
        var process = new Process { StartInfo = CreateStartInfo(args), EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => { if (e.Data != null) onOutputLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) onOutputLine(e.Data); };

        Start(process);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    public static void TryKill(Process process)
    {
        try
        {
            // Pas l'arbre entier : une commande adb qui vient de démarrer le démon adb en est le parent,
            // et le tuer avec elle empêcherait adb de jamais voir le téléphone.
            if (!process.HasExited) process.Kill(entireProcessTree: false);
        }
        catch (InvalidOperationException) { } // déjà terminé
        catch (Win32Exception) { }            // accès refusé ou course avec la fin du processus
    }

    private void Start(Process process)
    {
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new AdbException(
                $"Impossible de lancer adb ({adbPath}). Place platform-tools dans third_party\\adb ou ajoute adb au PATH. ({ex.Message})");
        }
    }

    private ProcessStartInfo CreateStartInfo(IEnumerable<string> args)
    {
        var info = new ProcessStartInfo(adbPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        if (serial != null)
        {
            info.ArgumentList.Add("-s");
            info.ArgumentList.Add(serial);
        }

        foreach (string arg in args) info.ArgumentList.Add(arg);
        return info;
    }

    private static string Describe(string[] args) => string.Join(' ', args);
}
