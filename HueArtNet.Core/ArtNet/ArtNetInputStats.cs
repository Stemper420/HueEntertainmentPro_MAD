namespace HueArtNet.Core.ArtNet;

public sealed class ArtNetInputStats
{
  public long AcceptedPackets { get; init; }
  public long IgnoredPackets { get; init; }
  public double PacketsPerSecond { get; init; }
  public DateTime? LastAcceptedPacketAtUtc { get; init; }
  public IReadOnlyList<int> ActiveUniverses { get; init; } = Array.Empty<int>();
}
