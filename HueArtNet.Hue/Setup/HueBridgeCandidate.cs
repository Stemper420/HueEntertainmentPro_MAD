namespace HueArtNet.Hue.Setup;

public sealed record HueBridgeCandidate(string BridgeId, string IpAddress, int? Port)
{
  public string DisplayName => string.IsNullOrWhiteSpace(BridgeId)
    ? IpAddress
    : $"{BridgeId} ({IpAddress})";

  public override string ToString()
  {
    return DisplayName;
  }
}
