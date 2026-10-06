using System.Diagnostics;
using Avalonia;
using Avalonia.ReactiveUI;
using Nein.Extensions;
using OsuPlayer.Data.DataModels;
using OsuPlayer.Extensions;
using OsuPlayer.Interfaces.Service;
using OsuPlayer.IO.Importer;
using OsuPlayer.Modules.Audio.Engine;
using OsuPlayer.Modules.Audio.Interfaces;
using OsuPlayer.Network.API.NorthFox;
using OsuPlayer.Network.LastFm;
using OsuPlayer.Services;
using OsuPlayer.Windows;
using Splat;

namespace OsuPlayer;

internal static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            if (OperatingSystem.IsMacOS() && Directory.GetCurrentDirectory() == "/")
                Directory.SetCurrentDirectory(AppContext.BaseDirectory);

            var builder = BuildAvaloniaApp();

            Register(Locator.CurrentMutable, Locator.Current);

            builder.StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex) //If we have an unhandled exception we catch it here
        {
#if DEBUG
            // If we debug the application and an unhandled exception is thrown,
            // we need to initiate a break for the debugger, so we can debug the exception.
            // Because we handle it above, the application just closes and logs it.
            // This avoids opening the logs and, we can debug it directly.
            Debugger.Break();
#endif

            // Create crashlog for users
            UnhandledExceptionHandler.HandleException(ex);

            // Start the CrashHandler to display the error message to the user
            var crashHandlerDll = Path.Combine(AppContext.BaseDirectory, "OsuPlayer.CrashHandler.dll");
            var processStartInfo = new ProcessStartInfo(GetDotnetHostPath(), $"\"{crashHandlerDll}\"")
            {
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory
            };

            Process.Start(processStartInfo);
        }
    }

    /// <summary>
    /// Resolves the path to the dotnet host executable. Falls back to "dotnet" (resolved via PATH)
    /// if it can't be determined, e.g. when dotnet is installed in a non-default location like ~/.dotnet on macOS.
    /// </summary>
    private static string GetDotnetHostPath()
    {
        var hostName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";

        // Launched via `dotnet OsuPlayer.dll`: the current process is the host itself.
        var processPath = Environment.ProcessPath;
        if (processPath != null && Path.GetFileName(processPath).Equals(hostName, StringComparison.OrdinalIgnoreCase))
            return processPath;

        // Framework-dependent apps: runtime dir is <dotnet root>/shared/Microsoft.NETCore.App/<version>/
        var runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        var candidate = Path.GetFullPath(Path.Combine(runtimeDir, "..", "..", "..", hostName));
        if (File.Exists(candidate))
            return candidate;

        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrEmpty(dotnetRoot) && File.Exists(Path.Combine(dotnetRoot, hostName)))
            return Path.Combine(dotnetRoot, hostName);

        return "dotnet";
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    private static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .UseSkia()
            .UseReactiveUI()
            .With(new Win32PlatformOptions());
    }

    private static void Register(IMutableDependencyResolver services, IReadonlyDependencyResolver resolver)
    {
        services.RegisterLazySingleton<IAudioEngine>(() => new BassEngine());

        services.RegisterLazySingleton<IDbReaderFactory>(() => new DbReaderFactory());

        RegisterServices(services, resolver);

        services.RegisterLazySingleton<IPlayer>(() => new Player(
            audioEngine: resolver.GetRequiredService<IAudioEngine>(),
            songSourceProvider: resolver.GetRequiredService<ISongSourceProvider>(),
            shuffleProvider: resolver.GetService<IShuffleServiceProvider>(),
            statisticsProvider: resolver.GetService<IStatisticsProvider>(),
            sortProvider: resolver.GetService<ISortProvider>(),
            historyProvider: resolver.GetService<IHistoryProvider>(),
            discordService: resolver.GetService<IDiscordService>(),
            lastFmApi: resolver.GetService<ILastFmApiService>()
        ));

        services.Register(() => new FluentAppWindowViewModel(
            resolver.GetRequiredService<IAudioEngine>(),
            resolver.GetRequiredService<IPlayer>(),
            resolver.GetRequiredService<IProfileManagerService>(),
            resolver.GetService<IShuffleServiceProvider>(),
            resolver.GetService<IStatisticsProvider>(),
            resolver.GetService<ISortProvider>(),
            resolver.GetService<IHistoryProvider>()));

        services.RegisterLazySingleton(() => new FluentAppWindow(
            resolver.GetRequiredService<FluentAppWindowViewModel>(),
            resolver.GetRequiredService<ILoggingService>()));
    }

    private static void RegisterServices(IMutableDependencyResolver services, IReadonlyDependencyResolver resolver)
    {
        services.RegisterLazySingletonAnd<IJsonService>(() => new JsonService());

        services.RegisterLazySingleton<ILoggingService>(() => new LoggingService());

        services.RegisterLazySingleton<IDiscordService>(() => new DiscordService());
        services.RegisterLazySingleton<IProfileManagerService>(() => new ProfileManagerService());
        services.RegisterLazySingleton<IShuffleServiceProvider>(() => new ShuffleService());
        services.RegisterLazySingleton<IStatisticsProvider>(() => new ApiStatisticsService(resolver.GetService<IProfileManagerService>()));
        services.RegisterLazySingleton<ISortProvider>(() => new SortService());
        services.RegisterLazySingleton<ISongSourceProvider>(() => new OsuSongSourceService(resolver.GetService<ISortProvider>()));
        services.RegisterLazySingleton<IHistoryProvider>(() => new HistoryService());
        services.RegisterLazySingleton<ILastFmApiService>(() => new LastFmService(new LastFmApi()));

        services.RegisterLazySingleton<IOsuPlayerApiService>(() => new NorthFox());
    }
}