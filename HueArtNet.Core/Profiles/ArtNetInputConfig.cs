namespace HueArtNet.Core.Profiles;

public sealed class ArtNetInputConfig
{
  public string? BindAddress { get; set; }
  public int Port { get; set; } = 6454;
}
