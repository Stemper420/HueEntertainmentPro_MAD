using HueArtNet.Core.Profiles;

namespace HueArtNet.Hue.Runtime;

public interface IHueHubSessionFactory
{
  IHueHubSession Create(HubMapping mapping);
}
