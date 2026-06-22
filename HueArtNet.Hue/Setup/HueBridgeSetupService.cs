namespace HueArtNet.Hue.Setup;

public sealed class HueBridgeSetupService : IHueBridgeSetupService
{
  private readonly IHueBridgeSetupGateway gateway;

  public HueBridgeSetupService(IHueBridgeSetupGateway gateway)
  {
    this.gateway = gateway;
  }

  public async Task<IReadOnlyList<HueBridgeCandidate>> DiscoverAsync(TimeSpan timeout, CancellationToken cancellationToken)
  {
    var bridges = await gateway.DiscoverAsync(timeout, cancellationToken);
    return bridges
      .GroupBy(x => $"{x.BridgeId}|{x.IpAddress}", StringComparer.OrdinalIgnoreCase)
      .Select(x => x.First())
      .OrderBy(x => ParseIpSortKey(x.IpAddress))
      .ThenBy(x => x.IpAddress, StringComparer.OrdinalIgnoreCase)
      .ToList();
  }

  public Task<HueBridgePairingResult> PairAsync(
    HueBridgeCandidate bridge,
    string applicationName,
    string deviceName,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(bridge.IpAddress))
      throw new ArgumentException("Bridge IP address is required.", nameof(bridge));

    return gateway.PairAsync(bridge, applicationName, deviceName, cancellationToken);
  }

  private static long ParseIpSortKey(string ipAddress)
  {
    var parts = ipAddress.Split('.');
    if (parts.Length != 4)
      return long.MaxValue;

    long value = 0;
    foreach (var part in parts)
    {
      if (!byte.TryParse(part, out byte octet))
        return long.MaxValue;

      value = (value << 8) + octet;
    }

    return value;
  }
}
