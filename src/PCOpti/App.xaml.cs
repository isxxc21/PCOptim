using System.Windows;
using System.Windows.Threading;
using PCOpti.Models;
using PCOpti.Services;
using PCOpti.Tweaks;
using PCOpti.ViewModels;

namespace PCOpti;

/// <summary>Composition root: builds the services and view models by hand (no DI container needed at this size).</summary>
public partial class App : Application
{
    private SystemStatsService? _stats;

    /// <summary>
    /// "--smoke-test": open every page once (read-only) and exit with code 0, or 1 if any UI error occurred.
    /// Used to check that all XAML loads and renders. It never applies a tweak.
    /// </summary>
    private bool _smokeTest;
    private bool _smokeFailed;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _smokeTest = e.Args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase);
        HookGlobalErrorHandlers();
        // The first-run dialog would otherwise become MainWindow and end the app when it closes.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var settings = new SettingsService();
        ThemeService.Apply(settings.Current.Accent);
        var dialogs = new DialogService();

        if (!settings.Current.AcceptedDisclaimer && !_smokeTest)
        {
            var choice = dialogs.Choose("Before you start",
                "PCOpti changes Windows system settings (registry values, services, power plans, startup items and DNS).\n\n" +
                "Every change is backed up first and can be reverted from inside the app, and nothing runs until you click it. " +
                "But changing system settings always carries some risk, and you use PCOpti at your own risk. " +
                "It is provided as-is, without warranty (MIT License).",
                "I understand", "Exit");
            if (choice != 0) { Shutdown(); return; }
            settings.Current.AcceptedDisclaimer = true;
            settings.Save();
        }

        // ---- Core services ----
        var registry = new WindowsRegistry();
        var commands = new CommandRunner();
        var backups = new JsonBackupStore(Path.Combine(AppInfo.DataFolder, "backups.json"));
        var log = new JsonChangeLog(Path.Combine(AppInfo.DataFolder, "changelog.json"));
        var ctx = new TweakContext(registry, backups, log, commands, new Win32SystemNotifier());
        var tweaks = TweakCatalog.Create(ctx);

        var restore = new RestorePointService(commands, log);
        var gate = new SafetyGate(dialogs, restore, settings);
        var startup = new StartupService(registry, commands, backups, log);
        var network = new NetworkService(commands, registry, backups, log);
        var boost = new GameBoostService(log);
        var revertAll = new RevertAllService(tweaks, backups, startup, network, log);
        _stats = new SystemStatsService();
        _stats.Start();

        // ---- Pages (generated from the tweak catalog where possible) ----
        var home = new HomeViewModel(_stats, backups);

        var gaming = new TweakPageViewModel("Gaming Tweaks", "",
            "Each tweak is independent: apply only the ones you want. Every change is backed up first and has its own Revert.",
            "Honest expectations: on a well-configured modern PC most of these give small gains (a few percent, or smoother " +
            "frame times rather than higher FPS). The biggest wins are usually closing background apps and updating GPU drivers.",
            tweaks.Where(t => t.Category == TweakCategory.Gaming).Select(t => new TweakCardViewModel(t, gate, useToggle: false)));

        var services = new TweakPageViewModel("Services", "",
            "A short, curated list of services that are safe to set to Manual. Manual is not Disabled: Windows still starts " +
            "the service if something needs it. Critical Windows services are never listed here.",
            "Honest expectations: each of these saves only a few MB of memory or a fraction of a second at boot.",
            tweaks.Where(t => t.Category == TweakCategory.Services).Select(t => new TweakCardViewModel(t, gate, useToggle: true)));

        var startupPage = new StartupViewModel(startup, gate);
        var cleaner = new CleanerViewModel(CleanTargets.Create(), dialogs, log);
        var networkPage = new NetworkViewModel(network, gate, dialogs);
        var boostPage = new GameBoostViewModel(boost, dialogs);
        var backup = new BackupViewModel(backups, log, revertAll, restore, settings, dialogs);
        var settingsPage = new SettingsViewModel(settings, new UpdateChecker());

        // After "Revert all", re-read the real state on the other pages.
        backup.RevertedAll += async (_, _) =>
        {
            await gaming.RefreshCommand.ExecuteAsync(null);
            await services.RefreshCommand.ExecuteAsync(null);
            await startupPage.RefreshCommand.ExecuteAsync(null);
        };

        var main = new MainViewModel(new PageViewModel[]
        {
            home, gaming, startupPage, cleaner, networkPage, services, boostPage, backup, settingsPage,
        });

        var window = new MainWindow { DataContext = main };
        MainWindow = window;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        window.Show();
        main.CurrentPage = home;

        if (_smokeTest)
        {
            _ = RunSmokeTestAsync(main, window);
            return;
        }

        // Optional, off by default. This is the only network call the app makes.
        if (settings.Current.CheckForUpdatesOnStartup)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var info = await new UpdateChecker().CheckAsync();
                    if (info.IsNewer) await Dispatcher.InvokeAsync(() => main.UpdateNotice = $"Update available: {info.TagName}");
                }
                catch { /* offline: stay quiet */ }
            });
        }
    }

    private async Task RunSmokeTestAsync(MainViewModel main, Window window)
    {
        foreach (var page in main.Pages)
        {
            main.CurrentPage = page;
            await Task.Delay(1500); // let the page load its data and render
            window.UpdateLayout();
            if (page.StatusIsError)
                File.AppendAllText(Path.Combine(AppInfo.DataFolder, "smoke-test.log"), $"{DateTime.Now:u} [{page.Title}] {page.StatusMessage}\n");
        }
        Shutdown(_smokeFailed ? 1 : 0);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _stats?.Dispose();
        base.OnExit(e);
    }

    /// <summary>Last line of defence: show the error and keep running instead of crashing.</summary>
    private void HookGlobalErrorHandlers()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            if (_smokeTest)
            {
                _smokeFailed = true;
                try { File.AppendAllText(Path.Combine(AppInfo.DataFolder, "smoke-test.log"), $"{DateTime.Now:u} {args.Exception}\n\n"); } catch { }
                return;
            }
            ShowCrash(args.Exception);
        };
        TaskScheduler.UnobservedTaskException += (_, args) => args.SetObserved();
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            try
            {
                File.AppendAllText(Path.Combine(AppInfo.DataFolder, "error.log"), $"{DateTime.Now:u} {args.ExceptionObject}\n\n");
            }
            catch { }
        };
    }

    private static void ShowCrash(Exception ex)
    {
        try
        {
            File.AppendAllText(Path.Combine(AppInfo.DataFolder, "error.log"), $"{DateTime.Now:u} {ex}\n\n");
            new DialogService().Info("Something went wrong",
                $"{ex.Message}\n\nPCOpti is still running. Details were saved to error.log in {AppInfo.DataFolder}.", isError: true);
        }
        catch { }
    }
}
