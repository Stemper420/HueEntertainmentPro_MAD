using ProtoBuf;

namespace HueLightDJ.Services.Interfaces.Models
{
  [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
  public class ArtNetBindAddress
  {
    public required string Address { get; set; }
    public required string Name { get; set; }
  }
}
