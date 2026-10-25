using System.IO;
using System.Text.Json;
using CodexLiveUsage.Core;
using CodexLiveUsage.Windows;

namespace CodexLiveUsage;

internal static class DiagnosticsProbe
{
    public static int WatchIpc(string outputPath, int seconds)
    {
        try
        {
            using var monitor = new CodexIpcActiveThreadMonitor();
            var deadline = DateTime.UtcNow.AddSeconds(Math.Clamp(seconds, 1, 60));
            var samples = new List<ActiveThreadStatus>();
            while (DateTime.UtcNow < deadline)
            {
                var status = monitor.GetStatus();
                if (samples.Count == 0 || status.Version != samples[^1].Version)
                {
                    samples.Add(status);
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                    File.WriteAllText(
                        outputPath,
                        JsonSerializer.Serialize(samples, new JsonSerializerOptions { WriteIndented = true }));
                }
                Thread.Sleep(100);
            }
            return 0;
        }
        catch (Exception error)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(
                outputPath,
                JsonSerializer.Serialize(new
                {
                    error = error.GetType().FullName,
                    message = error.Message,
                }, new JsonSerializerOptions { WriteIndented = true }));
            return 1;
        }
    }

    public static int Run(string outputPath)
    {
        try
        {
            var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
            if (string.IsNullOrWhiteSpace(codexHome))
            {
                codexHome = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".codex");
            }

            using var activeThreadMonitor = new CodexIpcActiveThreadMonitor();
            var deadline = DateTime.UtcNow.AddSeconds(3);
            ActiveThreadStatus route;
            do
            {
                route = activeThreadMonitor.GetStatus();
                if (route.ThreadId is not null)
                {
                    break;
                }
                Thread.Sleep(100);
            }
            while (DateTime.UtcNow < deadline);

            var monitor = new RolloutMetricsMonitor(codexHome);
            monitor.SelectThread(route.ThreadId);
            var snapshot = monitor.Poll();
            var window = CodexWindowLocator.Find();
            var composer = window is null ? null : ComposerLocator.Find(window);
            var payload = new
            {
                codexHome,
                ipc = route,
                snapshot,
                formattedText = snapshot is null ? null : MetricsFormatter.Format(snapshot),
                window = window is null
                    ? null
                    : new
                    {
                        handle = window.Handle.ToInt64(),
                        window.Bounds,
                        window.DpiScale,
                    },
                composer,
            };
            File.WriteAllText(
                outputPath,
                JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception error)
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                outputPath,
                JsonSerializer.Serialize(new
                {
                    error = error.GetType().FullName,
                    message = error.Message,
                }, new JsonSerializerOptions { WriteIndented = true }));
            return 1;
        }
    }
}
