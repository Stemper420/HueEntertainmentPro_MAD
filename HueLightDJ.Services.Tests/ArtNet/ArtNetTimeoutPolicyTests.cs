using HueLightDJ.Services.ArtNet;

namespace HueLightDJ.Services.Tests.ArtNet;

public class ArtNetTimeoutPolicyTests
{
  [Fact]
  public void IsTimedOut_returns_false_before_timeout_window()
  {
    DateTimeOffset now = DateTimeOffset.UtcNow;

    bool timedOut = ArtNetTimeoutPolicy.IsTimedOut(now.AddMilliseconds(-1500), now, TimeSpan.FromSeconds(2));

    Assert.False(timedOut);
  }

  [Fact]
  public void IsTimedOut_returns_true_after_timeout_window()
  {
    DateTimeOffset now = DateTimeOffset.UtcNow;

    bool timedOut = ArtNetTimeoutPolicy.IsTimedOut(now.AddMilliseconds(-2500), now, TimeSpan.FromSeconds(2));

    Assert.True(timedOut);
  }
}
