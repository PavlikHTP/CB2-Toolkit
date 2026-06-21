using System.IO;
using System.Windows;
using System.Windows.Threading;
using CB2Toolkit.Core.Services;

namespace CB2Toolkit.App;

public partial class App : Application
{
    public static string? ArgumentFilePath { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Length > 0)
        {
            try
            {
                string filePath = Path.GetFullPath(e.Args[0]);
                if (File.Exists(filePath) && Path.GetExtension(filePath).Equals(".as", StringComparison.OrdinalIgnoreCase))
                {
                    ArgumentFilePath = filePath;
                }
            }
            catch
            {
            }
        }

        base.OnStartup(e);

        DispatcherUnhandledException += OnUiException;
        AppDomain.CurrentDomain.UnhandledException += OnBackgroundException;
        TaskScheduler.UnobservedTaskException += OnAsyncTaskException;

        try
        {
            await SettingsService.Instance.LoadAsync();
            
            FileAssociationService.Register();

            if (!string.IsNullOrEmpty(ArgumentFilePath))
            {
                var settings = SettingsService.Instance.Current;
                string? folderPath = Path.GetDirectoryName(ArgumentFilePath);

                if (!string.IsNullOrEmpty(folderPath))
                {
                    settings.RecentAngelScriptFolders.Remove(folderPath);
                    settings.RecentAngelScriptFolders.Insert(0, folderPath);
                }

                settings.LastOpenedAngelScriptFilePath = ArgumentFilePath;
                await SettingsService.Instance.SaveAsync();

                if (Current.MainWindow is Views.MainWindow mainWin)
                {
                    mainWin.NavigateToEditor();
                }
            }

            DiscordRpcService.Initialize();
            _ = UpdateService.Instance.CheckForUpdatesAsync();
        }
        catch (Exception ex)
        {
            CrashReportingService.Instance.HandleException(ex);
            Environment.Exit(1);
        }
    }

    private void OnUiException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        CrashReportingService.Instance.HandleException(e.Exception);
        Environment.Exit(1);
    }

    private void OnBackgroundException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            CrashReportingService.Instance.HandleException(ex);
        }
        Environment.Exit(1);
    }

    private void OnAsyncTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        CrashReportingService.Instance.HandleException(e.Exception);
        e.SetObserved();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            DiscordRpcService.Shutdown();
        }
        catch
        {
        }
        base.OnExit(e);
    }
}