using HueArtNet.Hue.Setup;

namespace HueArtNet.Tests.Hue;

public class HueBridgeSetupServiceTests
{
  [Fact]
  public async Task DiscoverAsync_returns_distinct_bridges_sorted_by_ip()
  {
    var gateway = new FakeHueBridgeSetupGateway
    {
      DiscoveredBridges = new List<HueBridgeCandidate>
      {
        new("bridge-b", "192.168.1.30", 443),
        new("bridge-a", "192.168.1.20", 443),
        new("bridge-a", "192.168.1.20", 443)
      }
    };
    var service = new HueBridgeSetupService(gateway);

    var bridges = await service.DiscoverAsync(TimeSpan.FromSeconds(1), CancellationToken.None);

    Assert.Equal(new[] { "192.168.1.20", "192.168.1.30" }, bridges.Select(x => x.IpAddress));
  }

  [Fact]
  public async Task PairAsync_uses_gateway_and_returns_credentials_and_entertainment_groups()
  {
    var candidate = new HueBridgeCandidate("bridge-id", "192.168.1.20", 443);
    var expected = new HueBridgePairingResult(
      BridgeId: "bridge-id",
      BridgeIp: "192.168.1.20",
      BridgeName: "Studio bridge",
      ApplicationKey: "app-key",
      EntertainmentKey: "ent-key",
      EntertainmentGroups: new[]
      {
        new HueEntertainmentGroupInfo(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Desk", new[] { 1, 2, 3 })
      });
    var gateway = new FakeHueBridgeSetupGateway { PairingResult = expected };
    var service = new HueBridgeSetupService(gateway);

    var result = await service.PairAsync(candidate, "HueArtNet", "Desktop", CancellationToken.None);

    Assert.Equal(expected, result);
    Assert.Equal(candidate, gateway.PairedBridge);
    Assert.Equal("HueArtNet", gateway.ApplicationName);
    Assert.Equal("Desktop", gateway.DeviceName);
  }

  private sealed class FakeHueBridgeSetupGateway : IHueBridgeSetupGateway
  {
    public IReadOnlyList<HueBridgeCandidate> DiscoveredBridges { get; set; } = Array.Empty<HueBridgeCandidate>();
    public HueBridgePairingResult? PairingResult { get; set; }
    public HueBridgeCandidate? PairedBridge { get; private set; }
    public string? ApplicationName { get; private set; }
    public string? DeviceName { get; private set; }

    public Task<IReadOnlyList<HueBridgeCandidate>> DiscoverAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
      return Task.FromResult(DiscoveredBridges);
    }

    public Task<HueBridgePairingResult> PairAsync(
      HueBridgeCandidate bridge,
      string applicationName,
      string deviceName,
      CancellationToken cancellationToken)
    {
      PairedBridge = bridge;
      ApplicationName = applicationName;
      DeviceName = deviceName;
      return Task.FromResult(PairingResult ?? throw new InvalidOperationException("Pairing result was not configured."));
    }
  }
}
