using HueApi.ColorConverters;
using HueApi.Entertainment;
using HueApi.Entertainment.Extensions;
using HueApi.Entertainment.Models;
using HueApi.Extensions;
using HueApi.Models;
using HueArtNet.Core.ArtNet;
using HueArtNet.Core.Profiles;

namespace HueArtNet.Hue.Runtime;

public sealed class HueApiHubSession : IHueHubSession
{
  private readonly HubMapping mapping;
  private readonly object sync = new();
  private StreamingHueClient? client;
  private StreamingGroup? stream;
  private EntertainmentLayer? layer;
  private CancellationTokenSource? updateCts;
  private Dictionary<int, EntertainmentLight> lightsById = new();
  private bool isConnected;
  private string? lastError;

  public HueApiHubSession(HubMapping mapping)
  {
    this.mapping = mapping;
  }

  public IReadOnlyCollection<int> AvailableLightIds
  {
    get
    {
      lock (sync)
      {
        return lightsById.Keys.OrderBy(x => x).ToList();
      }
    }
  }

  public HueHubSessionStatus Status
  {
    get
    {
      lock (sync)
      {
        return new HueHubSessionStatus
        {
          Name = mapping.Name,
          BridgeIp = mapping.BridgeIp,
          IsConnected = isConnected,
          LastError = lastError
        };
      }
    }
  }

  public async Task ConnectAsync(CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(mapping.ApplicationKey))
      throw new InvalidOperationException($"{mapping.Name}: Hue application key is required.");

    if (string.IsNullOrWhiteSpace(mapping.EntertainmentKey))
      throw new InvalidOperationException($"{mapping.Name}: Hue entertainment key is required.");

    StreamingHueClient? newClient = null;
    try
    {
      updateCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
      newClient = new StreamingHueClient(mapping.BridgeIp, mapping.ApplicationKey, mapping.EntertainmentKey);
      var all = await newClient.LocalHueApi.EntertainmentConfiguration.GetAllAsync();
      var group = all.Data.FirstOrDefault(x => x.Id == mapping.EntertainmentGroupId)
        ?? throw new InvalidOperationException($"{mapping.Name}: Entertainment group {mapping.EntertainmentGroupId} was not found.");

      var locations = group.Channels.ToDictionary(x => x.ChannelId, x => x.Position);
      var streamLocations = locations.ToDictionary(
        x => x.Key,
        x => new Tuple<HuePosition, List<Guid>>(x.Value, new List<Guid>()));

      var newStream = new StreamingGroup(streamLocations);
      await newClient.LocalHueApi.SetStreamingAsync(mapping.EntertainmentGroupId, active: false);
      await newClient.ConnectAsync(mapping.EntertainmentGroupId, simulator: false);

      var newLayer = newStream.GetNewLayer(isBaseLayer: true);
      newLayer.AutoCalculateEffectUpdate(updateCts.Token);
      _ = newClient.AutoUpdateAsync(newStream, updateCts.Token, 50, onlySendDirtyStates: false);

      lock (sync)
      {
        client = newClient;
        stream = newStream;
        layer = newLayer;
        lightsById = newLayer.ToDictionary(x => (int)x.Id);
        isConnected = true;
        lastError = null;
      }

      newClient = null;
    }
    catch (Exception ex)
    {
      await CleanupPartiallyStartedClientAsync(newClient);
      lock (sync)
      {
        lastError = ex.Message;
        isConnected = false;
      }

      await StopAsync();
      throw;
    }
  }

  private async Task CleanupPartiallyStartedClientAsync(StreamingHueClient? partiallyStartedClient)
  {
    if (partiallyStartedClient == null)
      return;

    try
    {
      await partiallyStartedClient.LocalHueApi.SetStreamingAsync(mapping.EntertainmentGroupId, active: false);
      partiallyStartedClient.Close();
    }
    catch
    {
    }
  }

  public Task ApplyOutputsAsync(IReadOnlyList<ArtNetLightOutput> outputs, CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    lock (sync)
    {
      if (!isConnected || layer == null)
        return Task.CompletedTask;

      foreach (var output in outputs)
      {
        if (!lightsById.TryGetValue(output.LightId, out var light))
          continue;

        light.State.SetRGBColor(new RGBColor(output.HexColor));
        light.State.SetBrightness(output.Brightness);
      }
    }

    return Task.CompletedTask;
  }

  public async Task StopAsync()
  {
    StreamingHueClient? localClient;
    CancellationTokenSource? localCts;
    lock (sync)
    {
      localClient = client;
      localCts = updateCts;
      client = null;
      stream = null;
      layer = null;
      lightsById = new Dictionary<int, EntertainmentLight>();
      updateCts = null;
      isConnected = false;
    }

    try
    {
      localCts?.Cancel();
      if (localClient != null)
      {
        await localClient.LocalHueApi.SetStreamingAsync(mapping.EntertainmentGroupId, active: false);
        localClient.Close();
      }
    }
    catch (Exception ex)
    {
      lock (sync)
      {
        lastError = ex.Message;
      }
    }
    finally
    {
      localCts?.Dispose();
    }
  }
}
