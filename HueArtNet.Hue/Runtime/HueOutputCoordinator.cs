using HueArtNet.Core.ArtNet;
using HueArtNet.Core.Profiles;

namespace HueArtNet.Hue.Runtime;

public sealed class HueOutputCoordinator
{
  private readonly IHueHubSessionFactory sessionFactory;
  private readonly object sync = new();
  private Dictionary<Guid, IHueHubSession> sessionsByMappingId = new();
  private List<HubMapping> activeMappings = new();
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
    var startedByMappingId = new Dictionary<Guid, IHueHubSession>();
    var enabledMappings = profile.HubMappings.Where(x => x.Enabled).ToList();
    try
    {
      foreach (var mapping in enabledMappings)
      {
        cancellationToken.ThrowIfCancellationRequested();
        var session = sessionFactory.Create(mapping);
        await session.ConnectAsync(cancellationToken);
        started.Add(session);
        startedByMappingId.Add(mapping.Id, session);
      }

      lock (sync)
      {
        sessionsByMappingId = new Dictionary<Guid, IHueHubSession>(startedByMappingId);
        activeMappings = enabledMappings.ToList();
        currentProfile = profile;
      }
    }
    catch
    {
      await StopSessionsAsync(started);
      lock (sync)
      {
        sessionsByMappingId = new Dictionary<Guid, IHueHubSession>();
        activeMappings = new List<HubMapping>();
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
      activeSessions = sessionsByMappingId.Values.ToList();
      sessionsByMappingId = new Dictionary<Guid, IHueHubSession>();
      activeMappings = new List<HubMapping>();
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
        IsRunning = sessionsByMappingId.Count > 0,
        Sessions = activeMappings
          .Select(mapping => sessionsByMappingId.TryGetValue(mapping.Id, out var session) ? session.Status : null)
          .Where(status => status != null)
          .Select(status => status!)
          .ToList()
      };
    }
  }

  public async Task ApplyArtNetFramesAsync(IReadOnlyCollection<ArtDmxFrame> frames, CancellationToken cancellationToken)
  {
    ShowProfile profile;
    List<HubMapping> mappings;
    Dictionary<Guid, IHueHubSession> sessionSnapshot;
    lock (sync)
    {
      if (currentProfile == null || sessionsByMappingId.Count == 0)
        return;

      profile = currentProfile;
      mappings = activeMappings.ToList();
      sessionSnapshot = new Dictionary<Guid, IHueHubSession>(sessionsByMappingId);
    }

    var framesByUniverse = frames.ToDictionary(x => x.Universe);
    foreach (var mapping in mappings)
    {
      if (!framesByUniverse.TryGetValue(mapping.Universe, out var frame))
        continue;

      if (!sessionSnapshot.TryGetValue(mapping.Id, out var session))
        continue;

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
