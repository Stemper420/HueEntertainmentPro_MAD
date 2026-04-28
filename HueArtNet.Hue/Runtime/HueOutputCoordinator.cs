using HueArtNet.Core.ArtNet;
using HueArtNet.Core.Profiles;

namespace HueArtNet.Hue.Runtime;

public sealed class HueOutputCoordinator
{
  private readonly IHueHubSessionFactory sessionFactory;
  private readonly object sync = new();
  private List<IHueHubSession> sessions = new();
  private ShowProfile? currentProfile;

  public HueOutputCoordinator(IHueHubSessionFactory sessionFactory)
  {
    this.sessionFactory = sessionFactory;
  }

  public async Task StartAsync(ShowProfile profile, CancellationToken cancellationToken)
  {
    await StopAsync();

    var validation = ShowProfileValidator.Validate(profile);
    if (!validation.IsValid)
      throw new InvalidOperationException(string.Join(Environment.NewLine, validation.Errors));

    var started = new List<IHueHubSession>();
    try
    {
      foreach (var mapping in profile.HubMappings.Where(x => x.Enabled))
      {
        cancellationToken.ThrowIfCancellationRequested();
        var session = sessionFactory.Create(mapping);
        await session.ConnectAsync(cancellationToken);
        started.Add(session);
      }

      lock (sync)
      {
        sessions = started.ToList();
        currentProfile = profile;
      }
    }
    catch
    {
      await StopSessionsAsync(started);
      lock (sync)
      {
        sessions = new List<IHueHubSession>();
        currentProfile = null;
      }

      throw;
    }
  }

  public async Task StopAsync()
  {
    List<IHueHubSession> activeSessions;
    lock (sync)
    {
      activeSessions = sessions;
      sessions = new List<IHueHubSession>();
      currentProfile = null;
    }

    await StopSessionsAsync(activeSessions);
  }

  public HueOutputStatus GetStatus()
  {
    lock (sync)
    {
      return new HueOutputStatus
      {
        IsRunning = sessions.Count > 0,
        Sessions = sessions.Select(x => x.Status).ToList()
      };
    }
  }

  public async Task ApplyArtNetFramesAsync(IReadOnlyCollection<ArtDmxFrame> frames, CancellationToken cancellationToken)
  {
    ShowProfile profile;
    List<IHueHubSession> activeSessions;
    lock (sync)
    {
      if (currentProfile == null || sessions.Count == 0)
        return;

      profile = currentProfile;
      activeSessions = sessions.ToList();
    }

    var framesByUniverse = frames.ToDictionary(x => x.Universe);
    var enabledMappings = profile.HubMappings.Where(x => x.Enabled).ToList();

    for (int index = 0; index < enabledMappings.Count && index < activeSessions.Count; index++)
    {
      var mapping = enabledMappings[index];
      if (!framesByUniverse.TryGetValue(mapping.Universe, out var frame))
        continue;

      var session = activeSessions[index];
      var outputs = ArtNetDmxMapper.MapFrame(frame.Data, mapping, session.AvailableLightIds)
        .Select(output => ApplyBrightnessLimit(output, profile.Output.BrightnessLimit))
        .ToList();
      if (outputs.Count > 0)
        await session.ApplyOutputsAsync(outputs, cancellationToken);
    }
  }

  private static ArtNetLightOutput ApplyBrightnessLimit(ArtNetLightOutput output, double brightnessLimit)
  {
    return new ArtNetLightOutput
    {
      LightId = output.LightId,
      HexColor = output.HexColor,
      Brightness = Math.Min(output.Brightness, Math.Clamp(brightnessLimit, 0d, 1d))
    };
  }

  private static async Task StopSessionsAsync(IEnumerable<IHueHubSession> sessions)
  {
    foreach (var session in sessions)
    {
      try
      {
        await session.StopAsync();
      }
      catch
      {
      }
    }
  }
}
