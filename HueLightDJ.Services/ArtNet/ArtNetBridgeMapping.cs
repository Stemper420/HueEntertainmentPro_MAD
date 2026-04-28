using HueLightDJ.Services.Interfaces.Models;
using System.Collections.Generic;

namespace HueLightDJ.Services.ArtNet
{
  public class ArtNetBridgeMapping
  {
    public int Universe { get; set; }
    public int StartChannel { get; set; } = 1;
    public ArtNetFixtureMode FixtureMode { get; set; } = ArtNetFixtureMode.Rgb3;
    public List<int> LightOrder { get; set; } = new();
  }
}
