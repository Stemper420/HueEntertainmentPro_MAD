namespace HueArtNet.Core.Profiles;

public sealed class OutputConfig
{
  public int FramesPerSecond { get; set; } = 20;
  public double BrightnessLimit { get; set; } = 1d;
}
