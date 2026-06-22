using ProtoBuf;
using HueLightDJ.Services.Interfaces.Models;

namespace HueEntertainmentPro.Shared.Models
{

  [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
  public class BridgeGroupConnection
  {
    public Guid Id { get; set; }

    public required Bridge Bridge { get; set; }
    public Guid GroupId { get; set; }

    public string? Name { get; set; }

    public int ArtNetUniverse { get; set; }

    public int ArtNetStartChannel { get; set; } = 1;

    public ArtNetFixtureMode ArtNetFixtureMode { get; set; } = ArtNetFixtureMode.Rgb3;

    public List<int> ArtNetLightOrder { get; set; } = new();

  }
}
