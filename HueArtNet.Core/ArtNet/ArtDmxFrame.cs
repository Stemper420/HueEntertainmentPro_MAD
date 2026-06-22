namespace HueArtNet.Core.ArtNet;

public sealed class ArtDmxFrame
{
  public byte Sequence { get; init; }
  public byte Physical { get; init; }
  public int Universe { get; init; }
  public required byte[] Data { get; init; }
}
