using HueArtNet.Core.Profiles;

namespace HueArtNet.Hue.Runtime;

public sealed class HueApiHubSessionFactory : IHueHubSessionFactory
{
  public IHueHubSession Create(HubMapping mapping)
  {
    return new HueApiHubSession(mapping);
  }
}
