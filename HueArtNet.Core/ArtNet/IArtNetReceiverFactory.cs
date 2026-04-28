namespace HueArtNet.Core.ArtNet;

public interface IArtNetReceiverFactory
{
  IArtNetFrameSource Create(ArtNetReceiverOptions options);
}
