using System;

namespace HueLightDJ.Services.ArtNet
{
  public static class ArtNetTimeoutPolicy
  {
    public static bool IsTimedOut(DateTimeOffset? lastPacketAt, DateTimeOffset now, TimeSpan timeout)
    {
      return lastPacketAt.HasValue && now - lastPacketAt.Value >= timeout;
    }
  }
}
