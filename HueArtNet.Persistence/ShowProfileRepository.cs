using HueArtNet.Core.Profiles;
using HueArtNet.Persistence.Entities;
using HueArtNet.Persistence.Security;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace HueArtNet.Persistence;

public sealed class ShowProfileRepository : IShowProfileRepository
{
  private readonly HueArtNetDbContext dbContext;
  private readonly ICredentialProtector credentialProtector;

  public ShowProfileRepository(HueArtNetDbContext dbContext, ICredentialProtector credentialProtector)
  {
    this.dbContext = dbContext;
    this.credentialProtector = credentialProtector;
  }

  public async Task<IReadOnlyList<ShowProfile>> GetAllAsync(CancellationToken cancellationToken)
  {
    var entities = await dbContext.ShowProfiles
      .AsNoTracking()
      .Include(x => x.HubMappings)
      .OrderByDescending(x => x.IsDefault)
      .ThenBy(x => x.Name)
      .ToListAsync(cancellationToken);

    return entities.Select(ToModel).ToList();
  }

  public async Task<ShowProfile?> GetAsync(Guid id, CancellationToken cancellationToken)
  {
    var entity = await dbContext.ShowProfiles
      .AsNoTracking()
      .Include(x => x.HubMappings)
      .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    return entity == null ? null : ToModel(entity);
  }

  public async Task SaveAsync(ShowProfile profile, CancellationToken cancellationToken)
  {
    var validation = ShowProfileValidator.Validate(profile);
    if (!validation.IsValid)
      throw new InvalidOperationException(string.Join(Environment.NewLine, validation.Errors));

    var entity = await dbContext.ShowProfiles
      .FirstOrDefaultAsync(x => x.Id == profile.Id, cancellationToken);

    if (entity == null)
    {
      entity = new ShowProfileEntity { Id = profile.Id };
      dbContext.ShowProfiles.Add(entity);
    }
    else
    {
      await dbContext.HubMappings
        .Where(x => x.ShowProfileId == profile.Id)
        .ExecuteDeleteAsync(cancellationToken);
      DetachTrackedMappings(profile.Id);
    }

    Apply(profile, entity);
    dbContext.HubMappings.AddRange(profile.HubMappings.Select(mapping => ToEntity(profile.Id, mapping)));

    await dbContext.SaveChangesAsync(cancellationToken);
  }

  private void DetachTrackedMappings(Guid profileId)
  {
    foreach (var entry in dbContext.ChangeTracker.Entries<HubMappingEntity>().Where(x => x.Entity.ShowProfileId == profileId).ToList())
      entry.State = EntityState.Detached;
  }

  public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
  {
    var entity = await dbContext.ShowProfiles.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (entity == null)
      return;

    dbContext.ShowProfiles.Remove(entity);
    await dbContext.SaveChangesAsync(cancellationToken);
  }

  private static void Apply(ShowProfile profile, ShowProfileEntity entity)
  {
    entity.Name = profile.Name;
    entity.IsDefault = profile.IsDefault;
    entity.ArtNetBindAddress = profile.ArtNetInput.BindAddress;
    entity.ArtNetPort = profile.ArtNetInput.Port;
    entity.FailSafeMode = profile.FailSafe.Mode.ToString();
    entity.FailSafeTimeoutMilliseconds = (int)profile.FailSafe.Timeout.TotalMilliseconds;
    entity.FramesPerSecond = profile.Output.FramesPerSecond;
    entity.BrightnessLimit = profile.Output.BrightnessLimit;
  }

  private HubMappingEntity ToEntity(Guid profileId, HubMapping mapping)
  {
    return new HubMappingEntity
    {
      Id = mapping.Id,
      ShowProfileId = profileId,
      Name = mapping.Name,
      BridgeId = mapping.BridgeId,
      HueBridgeId = mapping.HueBridgeId,
      BridgeIp = mapping.BridgeIp,
      ProtectedApplicationKey = credentialProtector.Protect(mapping.ApplicationKey),
      ProtectedEntertainmentKey = credentialProtector.Protect(mapping.EntertainmentKey),
      EntertainmentGroupId = mapping.EntertainmentGroupId,
      Universe = mapping.Universe,
      StartChannel = mapping.StartChannel,
      FixtureMode = mapping.FixtureMode.ToString(),
      LightOrderJson = JsonSerializer.Serialize(mapping.LightOrder),
      Enabled = mapping.Enabled,
      Required = mapping.Required
    };
  }

  private ShowProfile ToModel(ShowProfileEntity entity)
  {
    return new ShowProfile
    {
      Id = entity.Id,
      Name = entity.Name,
      IsDefault = entity.IsDefault,
      ArtNetInput = new ArtNetInputConfig
      {
        BindAddress = entity.ArtNetBindAddress,
        Port = entity.ArtNetPort
      },
      FailSafe = new FailSafeConfig
      {
        Mode = Enum.TryParse<FailSafeMode>(entity.FailSafeMode, out var failSafeMode) ? failSafeMode : FailSafeMode.HoldLastFrame,
        Timeout = TimeSpan.FromMilliseconds(entity.FailSafeTimeoutMilliseconds)
      },
      Output = new OutputConfig
      {
        FramesPerSecond = entity.FramesPerSecond,
        BrightnessLimit = entity.BrightnessLimit
      },
      HubMappings = entity.HubMappings
        .OrderBy(x => x.Universe)
        .ThenBy(x => x.StartChannel)
        .Select(ToModel)
        .ToList()
    };
  }

  private HubMapping ToModel(HubMappingEntity entity)
  {
    return new HubMapping
    {
      Id = entity.Id,
      Name = entity.Name,
      BridgeId = entity.BridgeId,
      HueBridgeId = entity.HueBridgeId,
      BridgeIp = entity.BridgeIp,
      ApplicationKey = credentialProtector.Unprotect(entity.ProtectedApplicationKey),
      EntertainmentKey = credentialProtector.Unprotect(entity.ProtectedEntertainmentKey),
      EntertainmentGroupId = entity.EntertainmentGroupId,
      Universe = entity.Universe,
      StartChannel = entity.StartChannel,
      FixtureMode = Enum.TryParse<FixtureMode>(entity.FixtureMode, out var fixtureMode) ? fixtureMode : FixtureMode.Rgb3,
      LightOrder = JsonSerializer.Deserialize<List<int>>(entity.LightOrderJson) ?? new List<int>(),
      Enabled = entity.Enabled,
      Required = entity.Required
    };
  }
}
