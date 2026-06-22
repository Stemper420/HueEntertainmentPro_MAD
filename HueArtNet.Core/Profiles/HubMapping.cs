namespace HueArtNet.Core.Profiles;

public sealed class HubMapping
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public string Name { get; set; } = string.Empty;
  public Guid BridgeId { get; set; }
  public string HueBridgeId { get; set; } = string.Empty;
  public string BridgeIp { get; set; } = string.Empty;
  public string ApplicationKey { get; set; } = string.Empty;
  public string EntertainmentKey { get; set; } = string.Empty;
  public Guid EntertainmentGroupId { get; set; }
  public int Universe { get; set; }
  public int StartChannel { get; set; } = 1;
  public FixtureMode FixtureMode { get; set; } = FixtureMode.Rgb3;
  public List<int> LightOrder { get; set; } = new();
  public bool Enabled { get; set; } = true;
  public bool Required { get; set; }
}
