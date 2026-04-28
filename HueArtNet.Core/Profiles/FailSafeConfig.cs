namespace HueArtNet.Core.Profiles;

public sealed class FailSafeConfig
{
  public FailSafeMode Mode { get; set; } = FailSafeMode.HoldLastFrame;
  public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(2);
}
