using System.IO;

namespace CodexLiveUsage;

internal static class CrashLog
{
    private const long MaximumBytes = 1024 * 1024;

    public static void Write(string source, Exception? error)
    {
        try
        {
            var dataRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CodexLiveUsage");
            Directory.CreateDirectory(dataRoot);
            var path = Path.Combine(dataRoot, "error.log");
            if (File.Exists(path) && new FileInfo(path).Length >= MaximumBytes)
            {
                File.Move(path, path + ".old", overwrite: true);
            }

            File.AppendAllText(
                path,
                $"{DateTimeOffset.Now:O} [{source}] {error}{Environment.NewLine}");
        }
        catch
        {
            // Crash logging must never replace the original failure.
        }
    }
}
