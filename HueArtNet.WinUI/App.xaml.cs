using HueArtNet.Core.ArtNet;
using HueArtNet.Hue.Runtime;
using HueArtNet.Hue.Setup;
using HueArtNet.Persistence;
using HueArtNet.Persistence.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;

namespace HueArtNet.WinUI;

public partial class App : Application
{
  private readonly IHost host;
  private IServiceScope? windowScope;
  private Window? window;
  private bool shutdownStarted;

  public App()
  {
    InitializeComponent();
    UnhandledException += App_UnhandledException;
    AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    host = Host.CreateDefaultBuilder()
      .ConfigureServices(services =>
      {
        string dataDirectory = Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
          "HueArtNet");
        Directory.CreateDirectory(dataDirectory);
        string dbPath = Path.Combine(dataDirectory, "hue-artnet.db");

        services.AddDbContext<HueArtNetDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
        services.AddSingleton<ICredentialProtector, DpapiCredentialProtector>();
        services.AddScoped<IShowProfileRepository, ShowProfileRepository>();
        services.AddSingleton<IHueHubSessionFactory, HueApiHubSessionFactory>();
        services.AddSingleton<IHueBridgeSetupGateway, HueApiBridgeSetupGateway>();
        services.AddSingleton<IHueBridgeSetupService, HueBridgeSetupService>();
        services.AddSingleton<IArtNetReceiverFactory, ArtNetReceiverFactory>();
        services.AddSingleton<HueOutputCoordinator>();
        services.AddSingleton<ArtNetShowRuntime>();
        services.AddTransient<MainWindow>();
      })
      .Build();
  }

  private static void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
  {
    WriteCrashLog("XAML unhandled exception", e.Exception);
  }

  private static void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
  {
    WriteCrashLog("Domain unhandled exception", e.ExceptionObject as Exception);
  }

  private static void WriteCrashLog(string category, Exception? exception)
  {
    try
    {
      string dataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HueArtNet");
      Directory.CreateDirectory(dataDirectory);
      string logPath = Path.Combine(dataDirectory, "crash.log");
      File.AppendAllText(
        logPath,
        $"{DateTimeOffset.Now:O} {category}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
    }
    catch
    {
    }
  }

  protected override async void OnLaunched(LaunchActivatedEventArgs args)
  {
    await host.StartAsync();
    using (var scope = host.Services.CreateScope())
    {
      var dbContext = scope.ServiceProvider.GetRequiredService<HueArtNetDbContext>();
      await HueArtNetDatabaseInitializer.EnsureCreatedAndMigratedAsync(dbContext);
    }

    windowScope = host.Services.CreateScope();
    window = windowScope.ServiceProvider.GetRequiredService<MainWindow>();
    window.Closed += Window_Closed;
    window.Activate();
  }

  private async void Window_Closed(object sender, WindowEventArgs args)
  {
    if (shutdownStarted)
      return;

    shutdownStarted = true;
    try
    {
      await host.Services.GetRequiredService<ArtNetShowRuntime>().StopAsync();
      await host.StopAsync(TimeSpan.FromSeconds(5));
    }
    finally
    {
      windowScope?.Dispose();
      host.Dispose();
    }
  }
}
