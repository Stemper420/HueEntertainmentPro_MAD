namespace HueLightDJ.Services.ArtNet
{
  public class ArtDmxFrame
  {
    public int Universe { get; init; }
    public byte Sequence { get; init; }
    public byte Physical { get; init; }
    public required byte[] Data { get; init; }
  }
}
