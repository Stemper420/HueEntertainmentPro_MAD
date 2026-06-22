using System.Net;

namespace HueArtNet.Core.ArtNet;

public sealed class ArtNetReceiverOptions
{
  public IPAddress BindAddress { get; set; } = IPAddress.Any;
  public int Port { get; set; } = 6454;
  public IReadOnlyCollection<int> Universes { get; set; } = Array.Empty<int>();
}
