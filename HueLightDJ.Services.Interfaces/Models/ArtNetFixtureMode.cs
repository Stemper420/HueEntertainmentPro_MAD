using ProtoBuf;

namespace HueLightDJ.Services.Interfaces.Models
{
  [ProtoContract]
  public enum ArtNetFixtureMode
  {
    [ProtoEnum]
    Rgb3 = 0,

    [ProtoEnum]
    Rgbww5 = 1,

    [ProtoEnum]
    DimmerRgbww6 = 2
  }
}
