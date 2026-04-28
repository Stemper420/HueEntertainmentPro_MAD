using HueApi;
using HueApi.Models.Requests;
using HueLightDJ.Services.Interfaces;
using HueLightDJ.Services.Interfaces.Models;
using HueLightDJ.Services.Interfaces.Models.Requests;
using ProtoBuf.Grpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HueLightDJ.Services
{
  public class HueSetupService : IHueSetupService
  {
    public async Task<EntertainmentGroupResult> GetEntertainmentGroupsAsync(HueSetupRequest request, CallContext context = default)
    {
      try
      {
        var hueClient = new LocalHueApi(request.Ip, request.Key);
        var entConfigsResult = await hueClient.EntertainmentConfiguration.GetAllAsync();

        var groups = entConfigsResult.Data.Select(x => new SimpleEntertainmentGroup()
        {
          Id = x.Id,
          Name = x.Metadata?.Name,
          LightCount = x.Locations.ServiceLocations.Count()
        });

        return new EntertainmentGroupResult
        {
          Groups = groups
        };
      }
      catch (UnauthorizedAccessException)
      {
        return new EntertainmentGroupResult
        {
          ErrorMessage = "Unauthorized. Please check if your key is correct."
        };
      }
      catch (Exception)
      {
        return new EntertainmentGroupResult()
        {
          ErrorMessage = $"Could not connect to {request.Ip}"
        };
      }
    }

    public async Task<EntertainmentGroupChannelsResult> GetEntertainmentGroupChannelsAsync(HueSetupRequest request, CallContext context = default)
    {
      if (!request.GroupId.HasValue)
        throw new ArgumentNullException("GroupId is null");

      try
      {
        var hueClient = new LocalHueApi(request.Ip, request.Key);
        var groupResult = await hueClient.EntertainmentConfiguration.GetByIdAsync(request.GroupId.Value);
        var group = groupResult.Data.FirstOrDefault();
        if (group == null)
          return new EntertainmentGroupChannelsResult { ErrorMessage = "Entertainment group not found." };

        var servicePositions = group.Locations.ServiceLocations
          .SelectMany(serviceLocation => serviceLocation.Positions.Select((position, index) => new
          {
            ServiceId = serviceLocation.Service?.Rid,
            PositionIndex = index,
            Position = position
          }))
          .ToList();

        var channels = group.Channels
          .OrderBy(channel => channel.ChannelId)
          .Select(channel =>
          {
            var service = servicePositions.FirstOrDefault(x => PositionsMatch(x.Position, channel.Position));
            return new EntertainmentChannelInfo
            {
              ChannelId = channel.ChannelId,
              EntertainmentId = service?.ServiceId,
              PositionIndex = service?.PositionIndex ?? channel.ChannelId,
              X = channel.Position.X,
              Y = channel.Position.Y,
              Z = channel.Position.Z
            };
          })
          .ToList();

        return new EntertainmentGroupChannelsResult { Channels = channels };
      }
      catch (UnauthorizedAccessException)
      {
        return new EntertainmentGroupChannelsResult
        {
          ErrorMessage = "Unauthorized. Please check if your key is correct."
        };
      }
      catch (Exception)
      {
        return new EntertainmentGroupChannelsResult()
        {
          ErrorMessage = $"Could not connect to {request.Ip}"
        };
      }
    }

    public async Task IdentifyGroupsAsync(HueSetupRequest request, CallContext context = default)
    {
      if (!request.GroupId.HasValue)
        throw new ArgumentNullException("GroupId is null");

      var localHueClient = new LocalHueApi(request.Ip, request.Key);

      //Turn all lights in this entertainment group on
      var result = await localHueClient.EntertainmentConfiguration.GetByIdAsync(request.GroupId.Value);
      var entServices = result.Data.First().Locations.ServiceLocations.Select(x => x.Service?.Rid).ToList();

      var allEntResources = await localHueClient.Entertainment.GetAllAsync();

      var renderResources = allEntResources.Data.Where(x => entServices.Contains(x.Id)).Select(x => x.RendererReference).ToList();

      var lights = renderResources.Where(x => x?.Rtype == "light").ToList();

      var update = new UpdateLight
      {
        Identify = new Identify()
      };

      foreach (var light in lights)
      {
        if (light == null)
          continue;

        var updateResult = await localHueClient.Light.UpdateAsync(light.Rid, update);
        await Task.Delay(100); //prevent rate limitiing
      }

    }

    public async Task IdentifyEntertainmentChannelAsync(IdentifyEntertainmentChannelRequest request, CallContext context = default)
    {
      var localHueClient = new LocalHueApi(request.Ip, request.Key);
      var groupResult = await localHueClient.EntertainmentConfiguration.GetByIdAsync(request.GroupId);
      var group = groupResult.Data.FirstOrDefault();
      if (group == null)
        return;

      var selectedChannel = group.Channels.FirstOrDefault(x => x.ChannelId == request.ChannelId);
      if (selectedChannel == null)
        return;

      var serviceLocation = group.Locations.ServiceLocations
        .FirstOrDefault(x => x.Positions.Any(position => PositionsMatch(position, selectedChannel.Position)));

      if (serviceLocation?.Service?.Rid == null)
      {
        await IdentifyGroupsAsync(new HueSetupRequest
        {
          Ip = request.Ip,
          Key = request.Key,
          GroupId = request.GroupId
        }, context);
        return;
      }

      var allResources = await localHueClient.Resource.GetAllAsync();
      var device = allResources.Data.Where(x => x.Id == serviceLocation.Service.Rid).Select(x => x.Owner?.Rid).FirstOrDefault();
      var lightDeviceId = allResources.Data.Where(x => x.Id == device).Select(x => x.Services?.Where(x => x.Rtype == "light").FirstOrDefault()?.Rid).FirstOrDefault();

      if (!lightDeviceId.HasValue)
        return;

      var update = new UpdateLight
      {
        Identify = new Identify()
      };

      await localHueClient.Light.UpdateAsync(lightDeviceId.Value, update);
    }

    private static bool PositionsMatch(HueApi.Models.HuePosition left, HueApi.Models.HuePosition right)
    {
      return Math.Abs(left.X - right.X) < 0.0001
        && Math.Abs(left.Y - right.Y) < 0.0001
        && Math.Abs(left.Z - right.Z) < 0.0001;
    }

    public async Task<IEnumerable<LocatedBridge>> LocateBridgesAsync(CallContext context = default)
    {
      var bridgeLocator = new HueApi.BridgeLocator.HttpBridgeLocator();
      IEnumerable<LocatedBridge> ips = new List<LocatedBridge>();

      try
      {
        var result = await bridgeLocator.LocateBridgesAsync(TimeSpan.FromSeconds(2));
        ips = result.Select(x => new LocatedBridge() { IpAddress = x.IpAddress, BridgeId = x.BridgeId, Port = x.Port });
      }
      catch { }

      return ips;
    }

    public async Task<Interfaces.Models.RegisterEntertainmentResult?> RegisterAsync(HueSetupRequest request, CallContext context = default)
    {
      request.Ip = request.Ip.Trim();

      if (request.Ip.Contains(":") || request.Ip.Contains("/"))
        throw new Exception($"Not a valid ip: {request.Ip}");

      try
      {

        var result = await LocalHueApi.RegisterAsync(request.Ip, "HueLightDJ", "Web", generateClientKey: true);
        if (result == null)
          return null;

        return new Interfaces.Models.RegisterEntertainmentResult()
        {
          Ip = request.Ip,
          StreamingClientKey = result.StreamingClientKey,
          Username = result.Username
        };
      }
      catch (Exception ex) when (ex.Message.Contains("link button not pressed", StringComparison.InvariantCultureIgnoreCase))
      {
        return new Interfaces.Models.RegisterEntertainmentResult()
        {
          Ip = request.Ip,
          ErrorMessage = "Link button not pressed. Please press the link button on the bridge and try again."
        };
      }
      catch (Exception)
      {
        return new Interfaces.Models.RegisterEntertainmentResult()
        {
          Ip = request.Ip,
          ErrorMessage = $"Could not connect to {request.Ip}"
        };
      }
    }
  }
}
