using HueEntertainmentPro.Database.Models;
using HueLightDJ.Services.Interfaces.Models;
using System.Text.Json;

namespace HueEntertainmentPro.Services.Extensions
{
  public static class ModelExtensions
  {
    public static Shared.Models.Bridge ToApiModel(this Bridge input)
    {
      return new Shared.Models.Bridge
      {
        Id = input.Id,
        BridgeId = input.BridgeId,
        Ip = input.Ip,
        Name = input.Name,
        Username = input.Username,
        StreamingClientKey = input.StreamingClientKey
      };
    }

    public static Shared.Models.ProArea ToApiModel(this ProArea area)
    {
      return new HueEntertainmentPro.Shared.Models.ProArea
      {
        Id = area.Id,
        Name = area.Name,
        ArtNetEnabled = area.ArtNetEnabled,
        ArtNetBindAddress = area.ArtNetBindAddress,
        ArtNetTimeoutMode = Enum.TryParse<ArtNetTimeoutMode>(area.ArtNetTimeoutMode, out var timeoutMode) ? timeoutMode : ArtNetTimeoutMode.Blackout,
        Connections = area.ProAreaBridgeGroups
           .Select(pg => new HueEntertainmentPro.Shared.Models.BridgeGroupConnection
           {
             Id = pg.Id,
             Bridge = pg.Bridge?.ToApiModel() ?? new(),
             GroupId = pg.GroupId,
             Name = pg.Name,
             ArtNetUniverse = pg.ArtNetUniverse,
             ArtNetStartChannel = pg.ArtNetStartChannel,
             ArtNetFixtureMode = Enum.TryParse<ArtNetFixtureMode>(pg.ArtNetFixtureMode, out var fixtureMode) ? fixtureMode : ArtNetFixtureMode.Rgb3,
             ArtNetLightOrder = DeserializeLightOrder(pg.ArtNetLightOrderJson)
           })
           .ToList()
      };
    }

    private static List<int> DeserializeLightOrder(string? json)
    {
      if (string.IsNullOrWhiteSpace(json))
        return new List<int>();

      try
      {
        return JsonSerializer.Deserialize<List<int>>(json) ?? new List<int>();
      }
      catch
      {
        return new List<int>();
      }
    }
  }
}
