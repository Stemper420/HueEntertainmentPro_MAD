namespace HueArtNet.Core.ArtNet;

public sealed class ArtNetReceiverFactory : IArtNetReceiverFactory
{
  public IArtNetFrameSource Create(ArtNetReceiverOptions options)
  {
    return new ArtNetReceiver(options);
  }
}
