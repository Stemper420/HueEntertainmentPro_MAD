using HueLightDJ.Services.Interfaces.Models;
using ProtoBuf;

namespace HueEntertainmentPro.Shared.Models.Requests
{
  [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
  public class UpdateProAreaArtNetRequest
  {
    public Guid ProAreaId { get; set; }
    public bool ArtNetEnabled { get; set; }
    public string? ArtNetBindAddress { get; set; }
    public ArtNetTimeoutMode ArtNetTimeoutMode { get; set; } = ArtNetTimeoutMode.Blackout;
    public List<BridgeGroupArtNetSettings> Connections { get; set; } = new();
  }

  [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
  public class BridgeGroupArtNetSettings
  {
    public Guid BridgeGroupConnectionId { get; set; }
    public int ArtNetUniverse { get; set; }
    public int ArtNetStartChannel { get; set; } = 1;
    public ArtNetFixtureMode ArtNetFixtureMode { get; set; } = ArtNetFixtureMode.Rgb3;
    public List<int> ArtNetLightOrder { get; set; } = new();
  }
}
