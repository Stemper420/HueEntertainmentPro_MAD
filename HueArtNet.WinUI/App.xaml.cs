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
