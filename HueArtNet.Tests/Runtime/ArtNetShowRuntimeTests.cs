using System.Net;
using HueArtNet.Core.ArtNet;
using HueArtNet.Core.Profiles;
using HueArtNet.Hue.Runtime;

namespace HueArtNet.Tests.Runtime;

public class ArtNetShowRuntimeTests
{
  [Fact]
  public async Task StartAsync_starts_hue_output_and_receiver_for_enabled_universes()
  {
    var profile = CreateProfile();
    var hueFactory = new FakeHueHubSessionFactory();
    var receiverFactory = new FakeArtNetReceiverFactory();
    var runtime = new ArtNetShowRuntime(new HueOutputCoordinator(hueFactory), receiverFactory);

    await runtime.StartAsync(profile, CancellationToken.None);

    Assert.Single(receiverFactory.CreatedReceivers);
    Assert.Equal(IPAddress.Loopback, receiverFactory.LastOptions!.BindAddress);
    Assert.Equal(6455, receiverFactory.LastOptions.Port);
    Assert.Equal(new[] { 0, 1, 3 }, receiverFactory.LastOptions.Universes.OrderBy(x => x));
    Assert.True(receiverFactory.CreatedReceivers[0].IsRunning);
    Assert.Equal(3, hueFactory.Sessions.Count);
    Assert.All(hueFactory.Sessions, session => Assert.True(session.ConnectCalled));
  }

  [Fact]
  public async Task ProcessLatestFramesAsync_maps_latest_receiver_frames_to_hue_output()
  {
    var profile = CreateProfile(hubCount: 1);
    var hueFactory = new FakeHueHubSessionFactory();
    var receiverFactory = new FakeArtNetReceiverFactory();
    var runtime = new ArtNetShowRuntime(new HueOutputCoordinator(hueFactory), receiverFactory);
    await runtime.StartAsync(profile, CancellationToken.None);
    var receiver = receiverFactory.CreatedReceivers[0];
    receiver.FrameBuffer.AddFrame(new ArtDmxFrame
    {
      Universe = 0,
      Sequence = 1,
      Physical = 0,
      Data = new byte[] { 255, 0, 0, 0, 255, 0 }
    }, DateTimeOffset.UtcNow);

    await runtime.ProcessLatestFramesAsync(CancellationToken.None);

    Assert.Equal(2, hueFactory.Sessions[0].Outputs.Count);
    Assert.Equal("FF0000", hueFactory.Sessions[0].Outputs[0].HexColor);
    Assert.Equal("00FF00", hueFactory.Sessions[0].Outputs[1].HexColor);
  }

  [Fact]
  public async Task StopAsync_stops_receiver_and_hue_output()
  {
    var hueFactory = new FakeHueHubSessionFactory();
    var receiverFactory = new FakeArtNetReceiverFactory();
    var runtime = new ArtNetShowRuntime(new HueOutputCoordinator(hueFactory), receiverFactory);
    await runtime.StartAsync(CreateProfile(hubCount: 1), CancellationToken.None);

    await runtime.StopAsync();

    Assert.False(receiverFactory.CreatedReceivers[0].IsRunning);
    Assert.True(hueFactory.Sessions[0].StopCalled);
    Assert.False(runtime.GetStatus().IsRunning);
  }

  [Fact]
  public async Task ProcessLatestFramesAsync_applies_blackout_after_timeout_without_replaying_stale_frame()
  {
    var clock = new ManualTimeProvider(DateTimeOffset.Parse("2026-04-24T10:00:00Z"));
    var profile = CreateProfile(hubCount: 1);
    profile.FailSafe = new FailSafeConfig
    {
      Mode = FailSafeMode.Blackout,
      Timeout = TimeSpan.FromSeconds(2)
    };
    var hueFactory = new FakeHueHubSessionFactory();
    var receiverFactory = new FakeArtNetReceiverFactory();
    var runtime = new ArtNetShowRuntime(new HueOutputCoordinator(hueFactory), receiverFactory, clock);
    await runtime.StartAsync(profile, CancellationToken.None);
    var receiver = receiverFactory.CreatedReceivers[0];
    receiver.FrameBuffer.AddFrame(new ArtDmxFrame
    {
      Universe = 0,
      Sequence = 1,
      Physical = 0,
      Data = new byte[] { 255, 0, 0, 0, 255, 0 }
    }, clock.GetUtcNow());
    await runtime.ProcessLatestFramesAsync(CancellationToken.None);

    clock.Advance(TimeSpan.FromSeconds(3));
    await runtime.ProcessLatestFramesAsync(CancellationToken.None);

    Assert.Equal(4, hueFactory.Sessions[0].Outputs.Count);
    Assert.Equal("000000", hueFactory.Sessions[0].Outputs[2].HexColor);
    Assert.Equal(0, hueFactory.Sessions[0].Outputs[2].Brightness);
    Assert.Equal("000000", hueFactory.Sessions[0].Outputs[3].HexColor);
    Assert.Equal(0, hueFactory.Sessions[0].Outputs[3].Brightness);
    Assert.True(runtime.GetStatus().IsTimedOut);
  }

  [Fact]
  public async Task ProcessLatestFramesAsync_restarts_hue_output_after_restore_startup_timeout_when_new_frame_arrives()
  {
    var clock = new ManualTimeProvider(DateTimeOffset.Parse("2026-04-24T10:00:00Z"));
    var profile = CreateProfile(hubCount: 1);
    profile.FailSafe = new FailSafeConfig
    {
      Mode = FailSafeMode.RestoreStartupState,
      Timeout = TimeSpan.FromSeconds(2)
    };
    var hueFactory = new FakeHueHubSessionFactory();
    var receiverFactory = new FakeArtNetReceiverFactory();
    var runtime = new ArtNetShowRuntime(new HueOutputCoordinator(hueFactory), receiverFactory, clock);
    await runtime.StartAsync(profile, CancellationToken.None);
    var receiver = receiverFactory.CreatedReceivers[0];
    receiver.FrameBuffer.AddFrame(new ArtDmxFrame
    {
      Universe = 0,
      Sequence = 1,
      Physical = 0,
      Data = new byte[] { 255, 0, 0 }
    }, clock.GetUtcNow());
    await runtime.ProcessLatestFramesAsync(CancellationToken.None);

    clock.Advance(TimeSpan.FromSeconds(3));
    await runtime.ProcessLatestFramesAsync(CancellationToken.None);
    Assert.True(hueFactory.Sessions[0].StopCalled);
    Assert.True(runtime.GetStatus().IsTimedOut);

    receiver.FrameBuffer.AddFrame(new ArtDmxFrame
    {
      Universe = 0,
      Sequence = 2,
      Physical = 0,
      Data = new byte[] { 0, 0, 255 }
    }, clock.GetUtcNow());
    await runtime.ProcessLatestFramesAsync(CancellationToken.None);

    Assert.Equal(2, hueFactory.Sessions.Count);
    Assert.False(runtime.GetStatus().IsTimedOut);
    Assert.Single(hueFactory.Sessions[1].Outputs);
    Assert.Equal("0000FF", hueFactory.Sessions[1].Outputs[0].HexColor);
  }

  private static ShowProfile CreateProfile(int hubCount = 4)
  {
    return new ShowProfile
    {
      Name = "Runtime profile",
      ArtNetInput = new ArtNetInputConfig
      {
        BindAddress = IPAddress.Loopback.ToString(),
        Port = 6455
      },
      Output = new OutputConfig
      {
        FramesPerSecond = 30
      },
      HubMappings = Enumerable.Range(0, hubCount)
        .Select(index => new HubMapping
        {
          Name = $"Hub {index + 1}",
          BridgeId = Guid.NewGuid(),
          BridgeIp = $"192.168.1.{20 + index}",
          ApplicationKey = $"app-key-{index}",
          EntertainmentKey = $"ent-key-{index}",
          EntertainmentGroupId = Guid.NewGuid(),
          Universe = index == 2 ? 3 : index,
          StartChannel = 1,
          FixtureMode = FixtureMode.Rgb3,
          LightOrder = new List<int> { 1, 2 },
          Enabled = index != 3
        })
        .ToList()
    };
  }

  private sealed class FakeArtNetReceiverFactory : IArtNetReceiverFactory
  {
    public ArtNetReceiverOptions? LastOptions { get; private set; }
    public List<FakeArtNetFrameSource> CreatedReceivers { get; } = new();

    public IArtNetFrameSource Create(ArtNetReceiverOptions options)
    {
      LastOptions = options;
      var receiver = new FakeArtNetFrameSource(options.Universes);
      CreatedReceivers.Add(receiver);
      return receiver;
    }
  }

  private sealed class FakeArtNetFrameSource : IArtNetFrameSource
  {
    public FakeArtNetFrameSource(IReadOnlyCollection<int> universes)
    {
      FrameBuffer = new ArtNetFrameBuffer(universes);
    }

    public ArtNetFrameBuffer FrameBuffer { get; }
    public int BoundPort { get; private set; }
    public bool IsRunning { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
      IsRunning = true;
      BoundPort = 6455;
      return Task.CompletedTask;
    }

    public Task StopAsync()
    {
      IsRunning = false;
      return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
      IsRunning = false;
      return ValueTask.CompletedTask;
    }
  }

  private sealed class FakeHueHubSessionFactory : IHueHubSessionFactory
  {
    public List<FakeHueHubSession> Sessions { get; } = new();

    public IHueHubSession Create(HubMapping mapping)
    {
      var session = new FakeHueHubSession(mapping);
      Sessions.Add(session);
      return session;
    }
  }

  private sealed class FakeHueHubSession : IHueHubSession
  {
    private readonly HubMapping mapping;

    public FakeHueHubSession(HubMapping mapping)
    {
      this.mapping = mapping;
    }

    public bool ConnectCalled { get; private set; }
    public bool StopCalled { get; private set; }
    public List<ArtNetLightOutput> Outputs { get; } = new();

    public HueHubSessionStatus Status => new()
    {
      Name = mapping.Name,
      BridgeIp = mapping.BridgeIp,
      IsConnected = ConnectCalled && !StopCalled
    };

    public IReadOnlyCollection<int> AvailableLightIds { get; } = new[] { 1, 2 };

    public Task ConnectAsync(CancellationToken cancellationToken)
    {
      ConnectCalled = true;
      return Task.CompletedTask;
    }

    public Task ApplyOutputsAsync(IReadOnlyList<ArtNetLightOutput> outputs, CancellationToken cancellationToken)
    {
      Outputs.AddRange(outputs);
      return Task.CompletedTask;
    }

    public Task StopAsync()
    {
      StopCalled = true;
      return Task.CompletedTask;
    }
  }

  private sealed class ManualTimeProvider : TimeProvider
  {
    private DateTimeOffset now;

    public ManualTimeProvider(DateTimeOffset now)
    {
      this.now = now;
    }

    public override DateTimeOffset GetUtcNow()
    {
      return now;
    }

    public void Advance(TimeSpan value)
    {
      now = now.Add(value);
    }
  }
}
