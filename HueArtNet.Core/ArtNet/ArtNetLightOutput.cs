namespace HueArtNet.Core.ArtNet;

public sealed class ArtNetLightOutput
{
  public int LightId { get; init; }
  public required string HexColor { get; init; }
  public double Brightness { get; init; }
}
