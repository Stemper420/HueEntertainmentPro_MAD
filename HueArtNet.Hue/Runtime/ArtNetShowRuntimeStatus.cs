using HueArtNet.Core.ArtNet;

namespace HueArtNet.Hue.Runtime;

public sealed class ArtNetShowRuntimeStatus
{
  public bool IsRunning { get; init; }
  public bool IsTimedOut { get; init; }
  public string? ProfileName { get; init; }
  public string? BindAddress { get; init; }
  public int Port { get; init; }
  public string? LastError { get; init; }
  public ArtNetInputStats ArtNet { get; init; } = new();
  public HueOutputStatus HueOutput { get; init; } = new();
}
