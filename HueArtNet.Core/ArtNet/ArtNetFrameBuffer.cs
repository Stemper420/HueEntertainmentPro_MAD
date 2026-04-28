namespace HueArtNet.Core.ArtNet;

public sealed class ArtNetFrameBuffer
{
  private readonly object sync = new();
  private readonly HashSet<int> configuredUniverses;
  private readonly Dictionary<int, ArtDmxFrame> latestFrames = new();
  private readonly DateTimeOffset startedAt;
  private DateTimeOffset? lastAcceptedPacketAt;
  private long acceptedPackets;
  private long ignoredPackets;

  public ArtNetFrameBuffer(IEnumerable<int> configuredUniverses)
  {
    this.configuredUniverses = configuredUniverses.ToHashSet();
    startedAt = DateTimeOffset.UtcNow;
  }

  public bool AddFrame(ArtDmxFrame frame, DateTimeOffset receivedAt)
  {
    lock (sync)
    {
      if (!configuredUniverses.Contains(frame.Universe))
      {
        ignoredPackets++;
        return false;
      }

      latestFrames[frame.Universe] = Clone(frame);
      acceptedPackets++;
      lastAcceptedPacketAt = receivedAt;
      return true;
    }
  }

  public IReadOnlyList<ArtDmxFrame> ReadLatestFrames()
  {
    lock (sync)
    {
      return latestFrames.Values.Select(Clone).ToList();
    }
  }

  public ArtNetInputStats GetStats(DateTimeOffset now)
  {
    lock (sync)
    {
      double seconds = Math.Max((now - startedAt).TotalSeconds, 1d);
      return new ArtNetInputStats
      {
        AcceptedPackets = acceptedPackets,
        IgnoredPackets = ignoredPackets,
        PacketsPerSecond = acceptedPackets / seconds,
        LastAcceptedPacketAtUtc = lastAcceptedPacketAt?.UtcDateTime,
        ActiveUniverses = configuredUniverses.OrderBy(x => x).ToList()
      };
    }
  }

  private static ArtDmxFrame Clone(ArtDmxFrame frame)
  {
    return new ArtDmxFrame
    {
      Sequence = frame.Sequence,
      Physical = frame.Physical,
      Universe = frame.Universe,
      Data = frame.Data.ToArray()
    };
  }
}
