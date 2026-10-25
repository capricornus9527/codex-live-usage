using System.IO;
using System.Text.Json;
using CodexLiveUsage.Core;
using CodexLiveUsage.Windows;

namespace CodexLiveUsage;

internal static class DiagnosticsProbe
{
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

            var snapshot = new RolloutMetricsMonitor(codexHome).Poll();
            var window = CodexWindowLocator.Find();
            var composer = window is null ? null : ComposerLocator.Find(window);
            var payload = new
            {
                codexHome,
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
