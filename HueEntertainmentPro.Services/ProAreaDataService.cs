using HueEntertainmentPro.Database;
using HueEntertainmentPro.Database.Models;
using HueEntertainmentPro.Services.Extensions;
using HueEntertainmentPro.Shared.Interfaces;
using HueEntertainmentPro.Shared.Models.Requests;
using HueLightDJ.Services.Interfaces.Models;
using Microsoft.EntityFrameworkCore;
using ProtoBuf.Grpc;
using System.Text.Json;

namespace HueEntertainmentPro.Services
{
  public class ProAreaDataService(HueEntertainmentProDbContext dbContext) : IProAreaDataService
  {
    public static Guid demo1Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static Guid demo2Id = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public async Task<HueEntertainmentPro.Shared.Models.ProArea> AddBridgeGroup(AddBridgeGroupRequest req, CallContext context = default)
    {
      if (req.BridgeId == null || req.GroupId == null || req.ProAreaId == null)
        throw new ArgumentException("BridgeId and GroupId and ProAreaId are required.");

      // Find the bridge and group
      var bridge = await dbContext.Bridges.FirstOrDefaultAsync(b => b.Id == req.BridgeId.Value);
      var proArea = await dbContext.ProAreas.FirstOrDefaultAsync(b => b.Id == req.ProAreaId.Value);

      if (bridge == null || proArea == null || req.GroupId == null)
        throw new InvalidOperationException("Bridge or Group or ProArea not found.");

      // Find the ProArea for this group (assuming GroupId is unique per ProAreaBridgeGroup)


      var usedUniverses = await dbContext.ProAreaGroups
        .Where(pg => pg.ProAreaId == proArea.Id)
        .Select(pg => pg.ArtNetUniverse)
        .ToListAsync();

      int nextUniverse = Enumerable.Range(0, 32768)
        .First(universe => !usedUniverses.Contains(universe));

      // Add new bridge group connection
      var newGroup = new ProAreaBridgeGroup
      {
        Id = Guid.NewGuid(),
        ProAreaId = proArea.Id,
        BridgeId = bridge.Id,
        GroupId = req.GroupId.Value,
        Name = req.Name,
        ArtNetUniverse = nextUniverse,
        ArtNetStartChannel = 1,
        ArtNetFixtureMode = HueLightDJ.Services.Interfaces.Models.ArtNetFixtureMode.Rgb3.ToString(),
        CreatedDate = DateTime.UtcNow
      };
      dbContext.ProAreaGroups.Add(newGroup);

      await dbContext.SaveChangesAsync();

      var area = await GetProArea(new GuidRequest { Id = proArea.Id }, context);
      if (area == null)
      {
        throw new NullReferenceException($"Area is null. Id: {proArea.Id}");
      }
      return area;
    }

    public async Task DeleteBridgeGroup(GuidRequest req, CallContext context = default)
    {
      var group = await dbContext.ProAreaGroups.FirstOrDefaultAsync(pg => pg.Id == req.Id);
      if (group != null)
      {
        dbContext.ProAreaGroups.Remove(group);
        await dbContext.SaveChangesAsync();
      }
    }

    public async Task DeleteProArea(GuidRequest req, CallContext context = default)
    {
      var area = await dbContext.ProAreas.FirstOrDefaultAsync(pa => pa.Id == req.Id);
      if (area != null)
      {
        // Remove all related bridge groups
        var groups = dbContext.ProAreaGroups.Where(pg => pg.ProAreaId == area.Id);
        dbContext.ProAreaGroups.RemoveRange(groups);

        dbContext.ProAreas.Remove(area);
        await dbContext.SaveChangesAsync();
      }
    }

    public async Task<HueEntertainmentPro.Shared.Models.ProArea> UpdateProArea(UpdateProAreaRequest req, CallContext context = default)
    {
      var existing = await dbContext.ProAreas.Where(x => x.Id == req.Id).FirstOrDefaultAsync();
      if (existing != null)
      {
        existing.Name = req.Name;
        await dbContext.SaveChangesAsync();
      }

      var area = await GetProArea(new GuidRequest { Id = req.Id }, context);
      if (area == null)
      {
        throw new NullReferenceException($"Area is null. Id: {req.Id}");
      }
      return area;
    }

    public async Task<HueEntertainmentPro.Shared.Models.ProArea> UpdateProAreaArtNet(UpdateProAreaArtNetRequest req, CallContext context = default)
    {
      var existing = await dbContext.ProAreas
        .Include(x => x.ProAreaBridgeGroups)
        .FirstOrDefaultAsync(x => x.Id == req.ProAreaId);

      if (existing == null)
        throw new InvalidOperationException("Area not found.");

      if (req.ArtNetEnabled && existing.ProAreaBridgeGroups?.Any() != true)
        throw new ArgumentException("At least one entertainment group is required before Art-Net can be enabled.");

      existing.ArtNetEnabled = req.ArtNetEnabled;
      existing.ArtNetBindAddress = string.IsNullOrWhiteSpace(req.ArtNetBindAddress) ? null : req.ArtNetBindAddress.Trim();
      existing.ArtNetTimeoutMode = req.ArtNetTimeoutMode.ToString();

      var pendingMappings = new List<PendingArtNetMapping>();

      foreach (var connectionSettings in req.Connections)
      {
        var group = existing.ProAreaBridgeGroups?.FirstOrDefault(x => x.Id == connectionSettings.BridgeGroupConnectionId);
        if (group == null)
          continue;

        var lightOrder = connectionSettings.ArtNetLightOrder.Distinct().ToList();
        if (req.ArtNetEnabled && lightOrder.Count == 0)
          throw new ArgumentException($"{group.Name ?? group.GroupId.ToString()}: At least one Hue channel is required before Art-Net can be enabled.");

        string? error = ValidateArtNetMapping(
          connectionSettings.ArtNetUniverse,
          connectionSettings.ArtNetStartChannel,
          connectionSettings.ArtNetFixtureMode,
          Math.Max(lightOrder.Count, 1));
        if (error != null)
          throw new ArgumentException($"{group.Name ?? group.GroupId.ToString()}: {error}");

        group.ArtNetUniverse = connectionSettings.ArtNetUniverse;
        group.ArtNetStartChannel = connectionSettings.ArtNetStartChannel;
        group.ArtNetFixtureMode = connectionSettings.ArtNetFixtureMode.ToString();
        group.ArtNetLightOrderJson = JsonSerializer.Serialize(lightOrder);

        pendingMappings.Add(new PendingArtNetMapping(
          Name: group.Name ?? group.GroupId.ToString(),
          Universe: connectionSettings.ArtNetUniverse,
          StartChannel: connectionSettings.ArtNetStartChannel,
          FixtureMode: connectionSettings.ArtNetFixtureMode,
          LightCount: Math.Max(lightOrder.Count, 1)));
      }

      string? overlapError = ValidateArtNetOverlaps(pendingMappings);
      if (overlapError != null)
        throw new ArgumentException(overlapError);

      await dbContext.SaveChangesAsync();

      var area = await GetProArea(new GuidRequest { Id = req.ProAreaId }, context);
      if (area == null)
        throw new NullReferenceException($"Area is null. Id: {req.ProAreaId}");

      return area;
    }

    private static string? ValidateArtNetMapping(int universe, int startChannel, ArtNetFixtureMode fixtureMode, int lightCount)
    {
      if (universe < 0 || universe > 32767)
        return "Universe must be between 0 and 32767.";

      if (startChannel < 1 || startChannel > 512)
        return "Start channel must be between 1 and 512.";

      int channelWidth = fixtureMode switch
      {
        ArtNetFixtureMode.Rgb3 => 3,
        ArtNetFixtureMode.Rgbww5 => 5,
        ArtNetFixtureMode.DimmerRgbww6 => 6,
        _ => 3
      };

      int lastChannel = startChannel + (channelWidth * Math.Max(lightCount, 1)) - 1;
      return lastChannel > 512 ? "DMX block exceeds channel 512." : null;
    }

    private static string? ValidateArtNetOverlaps(IReadOnlyCollection<PendingArtNetMapping> mappings)
    {
      var ranges = mappings
        .Select(mapping => new
        {
          mapping.Name,
          mapping.Universe,
          First = mapping.StartChannel,
          Last = mapping.StartChannel + (GetFixtureWidth(mapping.FixtureMode) * Math.Max(mapping.LightCount, 1)) - 1
        })
        .Where(x => x.Last <= 512)
        .GroupBy(x => x.Universe);

      foreach (var universe in ranges)
      {
        var ordered = universe.OrderBy(x => x.First).ToList();
        for (int index = 1; index < ordered.Count; index++)
        {
          var previous = ordered[index - 1];
          var current = ordered[index];
          if (current.First <= previous.Last)
            return $"{current.Name}: DMX block {current.First}-{current.Last} overlaps {previous.Name} on universe {universe.Key}.";
        }
      }

      return null;
    }

    private static int GetFixtureWidth(ArtNetFixtureMode fixtureMode)
    {
      return fixtureMode switch
      {
        ArtNetFixtureMode.Rgb3 => 3,
        ArtNetFixtureMode.Rgbww5 => 5,
        ArtNetFixtureMode.DimmerRgbww6 => 6,
        _ => 3
      };
    }

    private sealed record PendingArtNetMapping(string Name, int Universe, int StartChannel, ArtNetFixtureMode FixtureMode, int LightCount);

    public async Task<HueEntertainmentPro.Shared.Models.ProArea> CreateProArea(CreateProAreaRequest req, CallContext context = default)
    {
      var newArea = new Database.Models.ProArea
      {
        Id = Guid.NewGuid(),
        Name = req.Name,
        CreatedDate = DateTime.UtcNow
      };

      dbContext.ProAreas.Add(newArea);
      await dbContext.SaveChangesAsync();

      var area = await GetProArea(new GuidRequest { Id = newArea.Id }, context);
      if (area == null)
      {
        throw new NullReferenceException($"Area is null. Id: {newArea.Id}");
      }
      return area;

    }

    public async Task<HueEntertainmentPro.Shared.Models.ProArea?> GetProArea(GuidRequest req, CallContext context = default)
    {
      if (req.Id == demo1Id)
      {
        return new HueEntertainmentPro.Shared.Models.ProArea
        {
          Id = demo1Id,
          Name = "Demo Area",
          ArtNetEnabled = false,
          Connections = new List<HueEntertainmentPro.Shared.Models.BridgeGroupConnection>
           {
              new Shared.Models.BridgeGroupConnection
              {
                Bridge =  new Shared.Models.Bridge
                {
                  Id = Guid.Empty,
                  Name = "Demo Bridge 1",
                  Ip = "demoLocations1",
                  StreamingClientKey = "demoLocations1",
                  Username = "demoLocations1"
                }
              },
              new Shared.Models.BridgeGroupConnection
              {
                Bridge =  new Shared.Models.Bridge
                {
                  Id = Guid.Empty,
                  Name = "Demo Bridge 2",
                  Ip = "demoLocations2",
                  StreamingClientKey = "demoLocations2",
                  Username = "demoLocations2"
                }
              }
          }
        };
      }

      if (req.Id == demo2Id)
      {
        return new HueEntertainmentPro.Shared.Models.ProArea
        {
          Id = demo1Id,
          Name = "Q42 Star Demo",
          ArtNetEnabled = false,
          Connections = new List<HueEntertainmentPro.Shared.Models.BridgeGroupConnection>
           {
              new Shared.Models.BridgeGroupConnection
              {
                Bridge =  new Shared.Models.Bridge
                {
                  Id = Guid.Empty,
                  Name = "Q42 Star Demo",
                  Ip = "sterDemoLocations1",
                  StreamingClientKey = "sterDemoLocations1",
                  Username = "sterDemoLocations1"
                }
              },
          }
        };
      }

      var area = await dbContext.ProAreas
        .Include(x => x.ProAreaBridgeGroups).ThenInclude(bg => bg.Bridge)
        .FirstOrDefaultAsync(pa => pa.Id == req.Id);

      if (area == null)
        return null;

      return area.ToApiModel();
    }

    public async Task<IEnumerable<HueEntertainmentPro.Shared.Models.ProArea>> GetProAreas(CallContext context = default)
    {
      var areas = await dbContext.ProAreas
        .Include(x => x.ProAreaBridgeGroups).ThenInclude(bg => bg.Bridge)
        .ToListAsync();

      return areas.Select(area => area.ToApiModel());
    }
  }
}
