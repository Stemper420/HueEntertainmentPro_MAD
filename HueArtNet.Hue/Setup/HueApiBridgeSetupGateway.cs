using HueApi;
using HueApi.BridgeLocator;
using HueApi.Models.Exceptions;

namespace HueArtNet.Hue.Setup;

public sealed class HueApiBridgeSetupGateway : IHueBridgeSetupGateway
{
  public async Task<IReadOnlyList<HueBridgeCandidate>> DiscoverAsync(TimeSpan timeout, CancellationToken cancellationToken)
  {
    var bridges = await HueBridgeDiscovery.FastDiscoveryWithNetworkScanFallbackAsync(
      timeout,
      TimeSpan.FromSeconds(Math.Max(timeout.TotalSeconds, 1)));

    cancellationToken.ThrowIfCancellationRequested();

    return bridges
      .Select(x => new HueBridgeCandidate(x.BridgeId, x.IpAddress, x.Port))
      .ToList();
  }

  public async Task<HueBridgePairingResult> PairAsync(
    HueBridgeCandidate bridge,
    string applicationName,
    string deviceName,
    CancellationToken cancellationToken)
  {
    try
    {
      var registration = await LocalHueApi.RegisterAsync(
        bridge.IpAddress,
        applicationName,
        deviceName,
        generateClientKey: true,
        cancellationToken);
      if (registration == null
        || string.IsNullOrWhiteSpace(registration.Username)
        || string.IsNullOrWhiteSpace(registration.StreamingClientKey))
        throw new InvalidOperationException("Hue bridge pairing did not return streaming credentials.");

      var localHueApi = new LocalHueApi(bridge.IpAddress, registration.Username, new HttpClient());
      var config = await localHueApi.GetConfigAsync()
        ?? throw new InvalidOperationException("Hue bridge configuration could not be loaded after pairing.");
      var groups = await localHueApi.EntertainmentConfiguration.GetAllAsync();
      var groupInfos = groups.Data
        .OrderBy(GetGroupName)
        .Select(x => new HueEntertainmentGroupInfo(
          x.Id,
          GetGroupName(x),
          (x.Channels ?? new()).Select(channel => channel.ChannelId).OrderBy(id => id).ToList()))
        .ToList();

      return new HueBridgePairingResult(
        BridgeId: string.IsNullOrWhiteSpace(config.BridgeId) ? bridge.BridgeId : config.BridgeId,
        BridgeIp: bridge.IpAddress,
        BridgeName: string.IsNullOrWhiteSpace(config.Name) ? bridge.IpAddress : config.Name,
        ApplicationKey: registration.Username,
        EntertainmentKey: registration.StreamingClientKey,
        EntertainmentGroups: groupInfos);
    }
    catch (LinkButtonNotPressedException ex)
    {
      throw new InvalidOperationException("Press the physical link button on the Hue bridge, then run pairing again.", ex);
    }
  }

  private static string GetGroupName(HueApi.Models.EntertainmentConfiguration configuration)
  {
    return string.IsNullOrWhiteSpace(configuration.Metadata?.Name)
      ? configuration.Id.ToString()
      : configuration.Metadata.Name;
  }
}
