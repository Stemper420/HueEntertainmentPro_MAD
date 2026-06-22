using ProtoBuf;

namespace HueLightDJ.Services.Interfaces.Models
{
  [ProtoContract]
  public enum ArtNetTimeoutMode
  {
    [ProtoEnum]
    Blackout = 0,

    [ProtoEnum]
    HoldLastFrame = 1,

    [ProtoEnum]
    ResumePreviousState = 2
  }
}
