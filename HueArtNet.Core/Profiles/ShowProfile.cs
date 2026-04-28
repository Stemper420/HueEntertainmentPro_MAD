namespace HueArtNet.Core.Profiles;

public sealed class ShowProfile
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public string Name { get; set; } = string.Empty;
  public bool IsDefault { get; set; }
  public ArtNetInputConfig ArtNetInput { get; set; } = new();
  public FailSafeConfig FailSafe { get; set; } = new();
  public OutputConfig Output { get; set; } = new();
  public List<HubMapping> HubMappings { get; set; } = new();
}
