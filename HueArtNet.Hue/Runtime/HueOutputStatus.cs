namespace HueArtNet.Hue.Runtime;

public sealed class HueOutputStatus
{
  public bool IsRunning { get; init; }
  public IReadOnlyList<HueHubSessionStatus> Sessions { get; init; } = Array.Empty<HueHubSessionStatus>();
}
