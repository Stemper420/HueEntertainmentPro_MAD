using HueArtNet.Core.Profiles;
using HueArtNet.Core.ArtNet;
using HueArtNet.Hue.Runtime;

namespace HueArtNet.Tests.Hue;

public class HueOutputCoordinatorTests
{
  [Fact]
  public async Task StartAsync_connects_all_enabled_hubs()
  {
    var profile = CreateProfile(4);
    var factory = new FakeHueHubSessionFactory();
    var coordinator = new HueOutputCoordinator(factory);

    await coordinator.StartAsync(profile, CancellationToken.None);

    Assert.Equal(4, factory.Sessions.Count);
    Assert.All(factory.Sessions, session => Assert.True(session.ConnectCalled));
    Assert.Equal(new[] { "Hub 1", "Hub 2", "Hub 3", "Hub 4" }, coordinator.GetStatus().Sessions.Select(x => x.Name));
  }

  [Fact]
  public async Task StartAsync_stops_started_sessions_when_a_later_hub_fails()
  {
    var profile = CreateProfile(4);
    var factory = new FakeHueHubSessionFactory(failBridgeIp: "192.168.1.22");
    var coordinator = new HueOutputCoordinator(factory);

    await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.StartAsync(profile, CancellationToken.None));

    Assert.Contains(factory.Sessions.Where(x => x.ConnectCalled), session => session.StopCalled);
    Assert.Empty(coordinator.GetStatus().Sessions);
  }

  [Fact]
  public async Task ApplyArtNetFramesAsync_maps_frames_to_sessions_by_universe()
  {
    var profile = CreateProfile(2);
    var factory = new FakeHueHubSessionFactory();
    var coordinator = new HueOutputCoordinator(factory);
    await coordinator.StartAsync(profile, CancellationToken.None);

    await coordinator.ApplyArtNetFramesAsync(new[]
    {
      new ArtDmxFrame { Universe = 1, Sequence = 1, Physical = 0, Data = new byte[] { 255, 0, 0, 0, 255, 0 } }
    }, CancellationToken.None);

    Assert.Empty(factory.Sessions[0].Outputs);
    Assert.Equal(2, factory.Sessions[1].Outputs.Count);
    Assert.Equal("FF0000", factory.Sessions[1].Outputs[0].HexColor);
    Assert.Equal("00FF00", factory.Sessions[1].Outputs[1].HexColor);
  }

  [Fact]
  public async Task ApplyArtNetFramesAsync_caps_brightness_to_profile_limit()
  {
    var profile = CreateProfile(1);
    profile.Output.BrightnessLimit = 0.4;
    var factory = new FakeHueHubSessionFactory();
    var coordinator = new HueOutputCoordinator(factory);
    await coordinator.StartAsync(profile, CancellationToken.None);

    await coordinator.ApplyArtNetFramesAsync(new[]
    {
      new ArtDmxFrame { Universe = 0, Sequence = 1, Physical = 0, Data = new byte[] { 255, 0, 0 } }
    }, CancellationToken.None);

    var output = Assert.Single(factory.Sessions[0].Outputs);
    Assert.Equal("FF0000", output.HexColor);
    Assert.Equal(0.4, output.Brightness, precision: 6);
  }

  private static ShowProfile CreateProfile(int hubCount)
  {
    return new ShowProfile
    {
      Name = "Runtime profile",
      HubMappings = Enumerable.Range(0, hubCount)
        .Select(index => new HubMapping
        {
          Name = $"Hub {index + 1}",
          BridgeId = Guid.NewGuid(),
          BridgeIp = $"192.168.1.{20 + index}",
          ApplicationKey = $"app-key-{index}",
          EntertainmentKey = $"ent-key-{index}",
          EntertainmentGroupId = Guid.NewGuid(),
          Universe = index,
          StartChannel = 1,
          FixtureMode = FixtureMode.Rgb3,
          LightOrder = new List<int> { 1, 2 },
          Enabled = true
        })
        .ToList()
    };
  }

  private sealed class FakeHueHubSessionFactory : IHueHubSessionFactory
  {
    private readonly string? failBridgeIp;

    public FakeHueHubSessionFactory(string? failBridgeIp = null)
    {
      this.failBridgeIp = failBridgeIp;
    }

    public List<FakeHueHubSession> Sessions { get; } = new();

    public IHueHubSession Create(HubMapping mapping)
    {
      var session = new FakeHueHubSession(mapping, mapping.BridgeIp == failBridgeIp);
      Sessions.Add(session);
      return session;
    }
  }

  private sealed class FakeHueHubSession : IHueHubSession
  {
    private readonly bool failConnect;

    public FakeHueHubSession(HubMapping mapping, bool failConnect)
    {
      Mapping = mapping;
      this.failConnect = failConnect;
    }

    public HubMapping Mapping { get; }
    public bool ConnectCalled { get; private set; }
    public bool StopCalled { get; private set; }

    public HueHubSessionStatus Status => new()
    {
      Name = Mapping.Name,
      BridgeIp = Mapping.BridgeIp,
      IsConnected = ConnectCalled && !StopCalled
    };

    public IReadOnlyCollection<int> AvailableLightIds { get; } = new[] { 1, 2 };
    public List<ArtNetLightOutput> Outputs { get; } = new();

    public Task ConnectAsync(CancellationToken cancellationToken)
    {
      ConnectCalled = true;
      if (failConnect)
        throw new InvalidOperationException("Bridge unavailable.");

      return Task.CompletedTask;
    }

    public Task StopAsync()
    {
      StopCalled = true;
      return Task.CompletedTask;
    }

    public Task ApplyOutputsAsync(IReadOnlyList<ArtNetLightOutput> outputs, CancellationToken cancellationToken)
    {
      Outputs.AddRange(outputs);
      return Task.CompletedTask;
    }
  }
}
