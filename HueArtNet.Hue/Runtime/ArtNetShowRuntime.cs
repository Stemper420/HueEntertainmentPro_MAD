using System.Net;
using HueArtNet.Core.ArtNet;
using HueArtNet.Core.Profiles;

namespace HueArtNet.Hue.Runtime;

public sealed class ArtNetShowRuntime
{
  private readonly HueOutputCoordinator outputCoordinator;
  private readonly IArtNetReceiverFactory receiverFactory;
  private readonly TimeProvider timeProvider;
  private readonly object sync = new();
  private IArtNetFrameSource? receiver;
  private CancellationTokenSource? pumpCts;
  private Task? pumpTask;
  private ShowProfile? currentProfile;
  private bool isTimedOut;
  private string? lastError;

  public ArtNetShowRuntime(
    HueOutputCoordinator outputCoordinator,
    IArtNetReceiverFactory receiverFactory,
    TimeProvider? timeProvider = null)
  {
    this.outputCoordinator = outputCoordinator;
    this.receiverFactory = receiverFactory;
    this.timeProvider = timeProvider ?? TimeProvider.System;
  }

  public async Task StartAsync(ShowProfile profile, CancellationToken cancellationToken)
  {
    await StopAsync();

    var validation = ShowProfileValidator.Validate(profile);
    if (!validation.IsValid)
      throw new InvalidOperationException(string.Join(Environment.NewLine, validation.Errors));

    var enabledMappings = profile.HubMappings.Where(x => x.Enabled).ToList();
    var options = new ArtNetReceiverOptions
    {
      BindAddress = ResolveBindAddress(profile.ArtNetInput.BindAddress),
      Port = profile.ArtNetInput.Port,
      Universes = enabledMappings.Select(x => x.Universe).Distinct().OrderBy(x => x).ToList()
    };

    IArtNetFrameSource? newReceiver = null;
    CancellationTokenSource? newPumpCts = null;
    try
    {
      await outputCoordinator.StartAsync(profile, cancellationToken);
      newReceiver = receiverFactory.Create(options);
      await newReceiver.StartAsync(cancellationToken);

      newPumpCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
      lock (sync)
      {
        receiver = newReceiver;
        pumpCts = newPumpCts;
        currentProfile = profile;
        isTimedOut = false;
        lastError = null;
      }

      pumpTask = Task.Run(() => PumpLoopAsync(newPumpCts.Token), CancellationToken.None);
    }
    catch
    {
      if (newPumpCts != null)
      {
        newPumpCts.Cancel();
        newPumpCts.Dispose();
      }

      if (newReceiver != null)
      {
        await newReceiver.StopAsync();
        await newReceiver.DisposeAsync();
      }

      await outputCoordinator.StopAsync();
      lock (sync)
      {
        receiver = null;
        pumpCts = null;
        pumpTask = null;
        currentProfile = null;
      }

      throw;
    }
  }

  public async Task StopAsync()
  {
    IArtNetFrameSource? localReceiver;
    CancellationTokenSource? localPumpCts;
    Task? localPumpTask;
    lock (sync)
    {
      localReceiver = receiver;
      localPumpCts = pumpCts;
      localPumpTask = pumpTask;
      receiver = null;
      pumpCts = null;
      pumpTask = null;
      currentProfile = null;
      isTimedOut = false;
    }

    if (localPumpCts != null)
    {
      try
      {
        localPumpCts.Cancel();
        if (localPumpTask != null)
          await localPumpTask;
      }
      catch (OperationCanceledException)
      {
      }
      finally
      {
        localPumpCts.Dispose();
      }
    }

    if (localReceiver != null)
    {
      await localReceiver.StopAsync();
      await localReceiver.DisposeAsync();
    }

    await outputCoordinator.StopAsync();
  }

  public async Task ProcessLatestFramesAsync(CancellationToken cancellationToken)
  {
    ShowProfile? profile;
    IArtNetFrameSource? activeReceiver;
    lock (sync)
    {
      profile = currentProfile;
      activeReceiver = receiver;
    }

    if (profile == null || activeReceiver == null)
      return;

    var now = timeProvider.GetUtcNow();
    var stats = activeReceiver.FrameBuffer.GetStats(now);
    if (IsTimedOut(profile, stats, now))
    {
      await ApplyTimeoutAsync(profile, cancellationToken);
      return;
    }

    var frames = activeReceiver.FrameBuffer.ReadLatestFrames();
    if (frames.Count == 0)
      return;

    if (ShouldRestartOutputAfterTimeout(profile))
      await outputCoordinator.StartAsync(profile, cancellationToken);

    await outputCoordinator.ApplyArtNetFramesAsync(frames, cancellationToken);
    lock (sync)
    {
      isTimedOut = false;
      lastError = null;
    }
  }

  public ArtNetShowRuntimeStatus GetStatus()
  {
    IArtNetFrameSource? activeReceiver;
    ShowProfile? profile;
    bool timedOut;
    string? error;
    lock (sync)
    {
      activeReceiver = receiver;
      profile = currentProfile;
      timedOut = isTimedOut;
      error = lastError;
    }

    return new ArtNetShowRuntimeStatus
    {
      IsRunning = activeReceiver?.IsRunning ?? false,
      IsTimedOut = timedOut,
      ProfileName = profile?.Name,
      BindAddress = profile?.ArtNetInput.BindAddress,
      Port = activeReceiver?.BoundPort ?? profile?.ArtNetInput.Port ?? 0,
      LastError = error,
      ArtNet = activeReceiver?.FrameBuffer.GetStats(timeProvider.GetUtcNow()) ?? new ArtNetInputStats(),
      HueOutput = outputCoordinator.GetStatus()
    };
  }

  private async Task PumpLoopAsync(CancellationToken cancellationToken)
  {
    while (!cancellationToken.IsCancellationRequested)
    {
      TimeSpan interval = GetPumpInterval();
      try
      {
        await Task.Delay(interval, timeProvider, cancellationToken);
        await ProcessLatestFramesAsync(cancellationToken);
      }
      catch (OperationCanceledException)
      {
        return;
      }
      catch (Exception ex)
      {
        lock (sync)
        {
          lastError = ex.Message;
        }
      }
    }
  }

  private TimeSpan GetPumpInterval()
  {
    ShowProfile? profile;
    lock (sync)
    {
      profile = currentProfile;
    }

    int fps = Math.Clamp(profile?.Output.FramesPerSecond ?? 20, 1, 100);
    return TimeSpan.FromMilliseconds(1000d / fps);
  }

  private async Task ApplyTimeoutAsync(ShowProfile profile, CancellationToken cancellationToken)
  {
    bool alreadyTimedOut;
    lock (sync)
    {
      alreadyTimedOut = isTimedOut;
    }

    if (!alreadyTimedOut && profile.FailSafe.Mode == FailSafeMode.Blackout)
    {
      var blackoutFrames = profile.HubMappings
        .Where(x => x.Enabled)
        .Select(x => x.Universe)
        .Distinct()
        .Select(universe => new ArtDmxFrame
        {
          Universe = universe,
          Sequence = 0,
          Physical = 0,
          Data = new byte[512]
        })
        .ToList();

      await outputCoordinator.ApplyArtNetFramesAsync(blackoutFrames, cancellationToken);
    }
    else if (!alreadyTimedOut && profile.FailSafe.Mode == FailSafeMode.RestoreStartupState)
    {
      await outputCoordinator.StopAsync();
    }

    lock (sync)
    {
      isTimedOut = true;
      lastError = "Art-Net input timed out.";
    }
  }

  private bool ShouldRestartOutputAfterTimeout(ShowProfile profile)
  {
    bool timedOut;
    lock (sync)
    {
      timedOut = isTimedOut;
    }

    return timedOut
      && profile.FailSafe.Mode == FailSafeMode.RestoreStartupState
      && !outputCoordinator.GetStatus().IsRunning;
  }

  private static bool IsTimedOut(ShowProfile profile, ArtNetInputStats stats, DateTimeOffset now)
  {
    if (!stats.LastAcceptedPacketAtUtc.HasValue)
      return false;

    var lastPacketAt = new DateTimeOffset(DateTime.SpecifyKind(stats.LastAcceptedPacketAtUtc.Value, DateTimeKind.Utc));
    return now - lastPacketAt >= profile.FailSafe.Timeout;
  }

  private static IPAddress ResolveBindAddress(string? configuredAddress)
  {
    if (!string.IsNullOrWhiteSpace(configuredAddress) && IPAddress.TryParse(configuredAddress, out var parsed))
      return parsed;

    return IPAddress.Any;
  }
}
