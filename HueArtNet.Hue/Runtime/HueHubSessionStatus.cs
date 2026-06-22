namespace HueArtNet.Hue.Runtime;

public sealed class HueHubSessionStatus
{
  public string Name { get; init; } = string.Empty;
  public string BridgeIp { get; init; } = string.Empty;
  public bool IsConnected { get; init; }
  public string? LastError { get; init; }
}
