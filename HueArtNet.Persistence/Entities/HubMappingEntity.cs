namespace HueArtNet.Persistence.Entities;

public sealed class HubMappingEntity
{
  public Guid Id { get; set; }
  public Guid ShowProfileId { get; set; }
  public ShowProfileEntity? ShowProfile { get; set; }
  public string Name { get; set; } = string.Empty;
  public Guid BridgeId { get; set; }
  public string HueBridgeId { get; set; } = string.Empty;
  public string BridgeIp { get; set; } = string.Empty;
  public string ProtectedApplicationKey { get; set; } = string.Empty;
  public string ProtectedEntertainmentKey { get; set; } = string.Empty;
  public Guid EntertainmentGroupId { get; set; }
  public int Universe { get; set; }
  public int StartChannel { get; set; }
  public string FixtureMode { get; set; } = string.Empty;
  public string LightOrderJson { get; set; } = "[]";
  public bool Enabled { get; set; }
  public bool Required { get; set; }
}
