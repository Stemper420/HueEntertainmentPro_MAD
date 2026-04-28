using HueApi.ColorConverters;
using HueApi.Entertainment.Extensions;
using HueApi.Entertainment.Models;
using HueLightDJ.Services.Interfaces;
using HueLightDJ.Services.Interfaces.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HueLightDJ.Services.ArtNet
{
  public class ArtNetInputService
  {
    public const int Port = 6454;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    private readonly IHubService hub;
    private readonly object sync = new();

    private CancellationTokenSource? cts;
    private UdpClient? udpClient;
    private Task? receiveTask;
    private Task? timeoutTask;
    private GroupConfiguration? currentConfig;
    private Dictionary<int, List<ArtNetRuntimeLightGroup>> groupsByUniverse = new();
    private List<LightSnapshot> previousStates = new();
    private DateTimeOffset startedAt;
    private DateTimeOffset? lastPacketAt;
    private long packetsReceived;
    private bool isListening;
    private bool isTimedOut;
    private string? bindAddress;
    private string? lastError;

    public ArtNetInputService(IHubService hub)
    {
      this.hub = hub;
    }

    public bool IsExclusiveActive
    {
      get
      {
        lock (sync)
        {
          return isListening;
        }
      }
    }

    public async Task StartAsync(GroupConfiguration config)
    {
      await StopAsync();

      if (!config.ArtNetEnabled)
        return;

      var runtimeGroups = StreamingSetup.GetArtNetRuntimeLightGroups()
        .Where(x => x.Connection.ArtNetUniverse >= 0)
        .GroupBy(x => x.Connection.ArtNetUniverse)
        .ToDictionary(x => x.Key, x => x.ToList());

      if (!runtimeGroups.Any())
      {
        const string error = "No active Hue groups are available for Art-Net.";
        lock (sync)
        {
          currentConfig = config;
          lastError = error;
        }

        await hub.SendAsync("StatusMsg", error);
        await hub.StatusChanged();
        return;
      }

      var address = ResolveBindAddress(config.ArtNetBindAddress);
      var endpoint = new IPEndPoint(address, Port);
      var receiver = new UdpClient(AddressFamily.InterNetwork);
      try
      {
        receiver.EnableBroadcast = true;
        receiver.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        receiver.Client.Bind(endpoint);
      }
      catch (Exception ex)
      {
        receiver.Dispose();
        var error = $"Unable to bind Art-Net UDP port {Port} on {FormatBindAddress(address)}: {ex.Message}";
        lock (sync)
        {
          currentConfig = config;
          groupsByUniverse = runtimeGroups;
          previousStates = new List<LightSnapshot>();
          startedAt = DateTimeOffset.UtcNow;
          lastPacketAt = null;
          packetsReceived = 0;
          isListening = false;
          isTimedOut = false;
          bindAddress = FormatBindAddress(address);
          lastError = error;
        }

        await hub.SendAsync("StatusMsg", error);
        await hub.StatusChanged();
        return;
      }

      var newCts = new CancellationTokenSource();
      var snapshots = CaptureSnapshots(runtimeGroups.Values.SelectMany(x => x));

      lock (sync)
      {
        cts = newCts;
        udpClient = receiver;
        currentConfig = config;
        groupsByUniverse = runtimeGroups;
        previousStates = snapshots;
        startedAt = DateTimeOffset.UtcNow;
        lastPacketAt = null;
        packetsReceived = 0;
        isListening = true;
        isTimedOut = false;
        bindAddress = FormatBindAddress(address);
        lastError = null;
      }

      receiveTask = Task.Run(() => ReceiveLoopAsync(receiver, newCts.Token));
      timeoutTask = Task.Run(() => TimeoutLoopAsync(newCts.Token));

      await hub.SendAsync("StatusMsg", $"Art-Net listening on {address}:{Port}");
      await hub.StatusChanged();
    }

    public async Task StopAsync()
    {
      CancellationTokenSource? localCts;
      UdpClient? localUdp;
      Task? localReceiveTask;
      Task? localTimeoutTask;

      lock (sync)
      {
        localCts = cts;
        localUdp = udpClient;
        localReceiveTask = receiveTask;
        localTimeoutTask = timeoutTask;

        cts = null;
        udpClient = null;
        receiveTask = null;
        timeoutTask = null;
        currentConfig = null;
        groupsByUniverse = new Dictionary<int, List<ArtNetRuntimeLightGroup>>();
        previousStates = new List<LightSnapshot>();
        isListening = false;
        isTimedOut = false;
      }

      if (localCts == null)
        return;

      try
      {
        localCts.Cancel();
        localUdp?.Close();
        var tasks = new[] { localReceiveTask, localTimeoutTask }
          .Where(x => x != null)
          .Cast<Task>();
        await Task.WhenAll(tasks);
      }
      catch
      {
      }
      finally
      {
        localCts.Dispose();
        localUdp?.Dispose();
        await hub.StatusChanged();
      }
    }

    public ArtNetStatusModel GetStatus()
    {
      lock (sync)
      {
        double seconds = Math.Max((DateTimeOffset.UtcNow - startedAt).TotalSeconds, 1d);
        return new ArtNetStatusModel
        {
          IsEnabled = currentConfig?.ArtNetEnabled ?? false,
          IsListening = isListening,
          IsTimedOut = isTimedOut,
          BindAddress = bindAddress,
          Port = Port,
          TimeoutMode = currentConfig?.ArtNetTimeoutMode ?? ArtNetTimeoutMode.Blackout,
          LastPacketAtUtc = lastPacketAt?.UtcDateTime,
          PacketsReceived = packetsReceived,
          PacketsPerSecond = packetsReceived / seconds,
          ActiveUniverses = groupsByUniverse.Keys.OrderBy(x => x).ToList(),
          LastError = lastError
        };
      }
    }

    public IEnumerable<ArtNetBindAddress> GetLocalBindAddresses()
    {
      foreach (var item in NetworkInterface.GetAllNetworkInterfaces()
        .Where(x => x.OperationalStatus == OperationalStatus.Up)
        .SelectMany(x => x.GetIPProperties().UnicastAddresses.Select(address => new { Network = x, Address = address.Address }))
        .Where(x => x.Address.AddressFamily == AddressFamily.InterNetwork)
        .OrderBy(x => IPAddress.IsLoopback(x.Address) ? 1 : 0)
        .ThenBy(x => x.Address.ToString()))
      {
        yield return new ArtNetBindAddress
        {
          Address = item.Address.ToString(),
          Name = $"{item.Network.Name} ({item.Address})"
        };
      }
    }

    private async Task ReceiveLoopAsync(UdpClient receiver, CancellationToken cancellationToken)
    {
      while (!cancellationToken.IsCancellationRequested)
      {
        try
        {
          var result = await receiver.ReceiveAsync(cancellationToken);
          if (!ArtDmxPacketParser.TryParse(result.Buffer, out var frame, out var error) || frame == null)
          {
            lock (sync)
            {
              lastError = error;
            }
            continue;
          }

          ApplyFrame(frame);
        }
        catch (OperationCanceledException)
        {
          return;
        }
        catch (ObjectDisposedException)
        {
          return;
        }
        catch (Exception ex)
        {
          lock (sync)
          {
            lastError = ex.Message;
          }
          await hub.SendAsync("StatusMsg", $"Art-Net error: {ex.Message}");
        }
      }
    }

    private async Task TimeoutLoopAsync(CancellationToken cancellationToken)
    {
      var lastStatusPush = DateTimeOffset.MinValue;

      while (!cancellationToken.IsCancellationRequested)
      {
        await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);

        if (DateTimeOffset.UtcNow - lastStatusPush >= TimeSpan.FromSeconds(1))
        {
          lastStatusPush = DateTimeOffset.UtcNow;
          await hub.StatusChanged();
        }

        GroupConfiguration? config;
        bool shouldTimeout;
        lock (sync)
        {
          config = currentConfig;
          shouldTimeout = isListening
            && !isTimedOut
            && ArtNetTimeoutPolicy.IsTimedOut(lastPacketAt, DateTimeOffset.UtcNow, Timeout);
        }

        if (!shouldTimeout || config == null)
          continue;

        switch (config.ArtNetTimeoutMode)
        {
          case ArtNetTimeoutMode.Blackout:
            ApplyBlackout();
            break;
          case ArtNetTimeoutMode.ResumePreviousState:
            RestorePreviousState();
            break;
        }

        lock (sync)
        {
          isTimedOut = true;
          lastError = "Art-Net input timed out.";
        }

        await hub.SendAsync("StatusMsg", "Art-Net input timed out.");
        await hub.StatusChanged();
      }
    }

    private void ApplyFrame(ArtDmxFrame frame)
    {
      List<ArtNetRuntimeLightGroup>? groups;
      lock (sync)
      {
        if (!groupsByUniverse.TryGetValue(frame.Universe, out groups))
          return;

        lastPacketAt = DateTimeOffset.UtcNow;
        packetsReceived++;
        isTimedOut = false;
        lastError = null;
        groups = groups.ToList();
      }

      foreach (var group in groups)
      {
        var mapping = new ArtNetBridgeMapping
        {
          Universe = group.Connection.ArtNetUniverse,
          StartChannel = group.Connection.ArtNetStartChannel,
          FixtureMode = group.Connection.ArtNetFixtureMode,
          LightOrder = group.Connection.ArtNetLightOrder
        };

        var lightsById = group.Lights.ToDictionary(x => (int)x.Id);
        foreach (var output in ArtNetDmxMapper.MapFrame(frame.Data, mapping, lightsById.Keys.ToList()))
        {
          if (!lightsById.TryGetValue(output.LightId, out var light))
            continue;

          light.State.SetRGBColor(new RGBColor(output.HexColor));
          light.State.SetBrightness(output.Brightness);
        }
      }
    }

    private void ApplyBlackout()
    {
      foreach (var light in GetCurrentLights())
      {
        light.State.SetRGBColor(new RGBColor("000000"));
        light.State.SetBrightness(0);
      }
    }

    private void RestorePreviousState()
    {
      foreach (var snapshot in previousStates)
      {
        snapshot.Light.State.SetRGBColor(new RGBColor(snapshot.HexColor));
        snapshot.Light.State.SetBrightness(snapshot.Brightness);
      }
    }

    private List<EntertainmentLight> GetCurrentLights()
    {
      lock (sync)
      {
        return groupsByUniverse.Values.SelectMany(x => x).SelectMany(x => x.Lights).Distinct().ToList();
      }
    }

    private static List<LightSnapshot> CaptureSnapshots(IEnumerable<ArtNetRuntimeLightGroup> groups)
    {
      return groups
        .SelectMany(x => x.Lights)
        .Distinct()
        .Select(light => new LightSnapshot
        {
          Light = light,
          HexColor = light.State.RGBColor.ToHex(),
          Brightness = light.State.Brightness
        })
        .ToList();
    }

    private IPAddress ResolveBindAddress(string? configuredAddress)
    {
      if (!string.IsNullOrWhiteSpace(configuredAddress) && IPAddress.TryParse(configuredAddress, out var parsed))
        return parsed;

      return IPAddress.Any;
    }

    private static string FormatBindAddress(IPAddress address)
    {
      return address.Equals(IPAddress.Any) ? "0.0.0.0 (all IPv4)" : address.ToString();
    }

    private class LightSnapshot
    {
      public required EntertainmentLight Light { get; init; }
      public required string HexColor { get; init; }
      public double Brightness { get; init; }
    }
  }
}
