using ProtoBuf;
using System;
using System.Collections.Generic;

namespace HueLightDJ.Services.Interfaces.Models
{
  [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
  public class ConnectionConfiguration
  {
    public required string Ip { get; set; }
    public required string Key { get; set; }
    public required string EntertainmentKey { get; set; }
    public Guid? GroupId { get; set; }
    public bool UseSimulator { get; set; }
    public int ArtNetUniverse { get; set; }
    public int ArtNetStartChannel { get; set; } = 1;
    public ArtNetFixtureMode ArtNetFixtureMode { get; set; } = ArtNetFixtureMode.Rgb3;
    public List<int> ArtNetLightOrder { get; set; } = new();
  }
}
