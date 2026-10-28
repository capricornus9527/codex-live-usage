using System.Windows;
using System.Windows.Threading;
using CodexLiveUsage.Windows;

namespace CodexLiveUsage;

internal static class Program
{
    private const string MutexName = @"Local\CodexLiveUsageCompanion";

    [STAThread]
    public static int Main(string[] args)
    {
        NativeMethods.TryEnablePerMonitorDpiAwareness();
        if (args.Length == 2 && args[0] == "--diagnostics")
        {
            return DiagnosticsProbe.Run(args[1]);
        }

        if (args.Length == 3 &&
            args[0] == "--ipc-watch" &&
            int.TryParse(args[2], out var seconds))
        {
            return DiagnosticsProbe.WatchIpc(args[1], seconds);
        }

        using var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            return 0;
        }

        var application = new Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown,
        };
        application.DispatcherUnhandledException += (_, error) =>
        {
            CrashLog.Write("Dispatcher", error.Exception);
            error.Handled = true;
            application.Shutdown(1);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, error) =>
        {
            CrashLog.Write("AppDomain", error.ExceptionObject as Exception);
        };
        TaskScheduler.UnobservedTaskException += (_, error) =>
        {
            CrashLog.Write("TaskScheduler", error.Exception);
            error.SetObserved();
        };

        AppController? controller = null;
        application.Startup += (_, _) =>
        {
            controller = new AppController();
            controller.Start();
        };
        application.Exit += (_, _) =>
        {
            controller?.Dispose();
            mutex.ReleaseMutex();
        };

        return application.Run();
    }
}
