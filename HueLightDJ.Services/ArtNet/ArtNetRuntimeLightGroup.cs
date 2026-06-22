using HueApi.Entertainment.Models;
using HueLightDJ.Services.Interfaces.Models;
using System.Collections.Generic;

namespace HueLightDJ.Services.ArtNet
{
  internal class ArtNetRuntimeLightGroup
  {
    public required ConnectionConfiguration Connection { get; init; }
    public required IReadOnlyList<EntertainmentLight> Lights { get; init; }
  }
}
