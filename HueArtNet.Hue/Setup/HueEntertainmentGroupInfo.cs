namespace HueArtNet.Hue.Setup;

public sealed record HueEntertainmentGroupInfo(Guid Id, string Name, IReadOnlyList<int> ChannelIds)
{
  public string DisplayName => $"{Name} ({ChannelIds.Count} channels)";

  public override string ToString()
  {
    return DisplayName;
  }
}
