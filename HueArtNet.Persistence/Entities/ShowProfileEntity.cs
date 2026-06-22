namespace HueArtNet.Persistence.Entities;

public sealed class ShowProfileEntity
{
  public Guid Id { get; set; }
  public string Name { get; set; } = string.Empty;
  public bool IsDefault { get; set; }
  public string? ArtNetBindAddress { get; set; }
  public int ArtNetPort { get; set; }
  public string FailSafeMode { get; set; } = string.Empty;
  public int FailSafeTimeoutMilliseconds { get; set; }
  public int FramesPerSecond { get; set; }
  public double BrightnessLimit { get; set; }
  public List<HubMappingEntity> HubMappings { get; set; } = new();
}
