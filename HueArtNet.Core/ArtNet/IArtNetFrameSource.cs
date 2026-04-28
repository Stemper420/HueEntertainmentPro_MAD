namespace HueArtNet.Core.ArtNet;

public interface IArtNetFrameSource : IAsyncDisposable
{
  ArtNetFrameBuffer FrameBuffer { get; }
  int BoundPort { get; }
  bool IsRunning { get; }
  Task StartAsync(CancellationToken cancellationToken);
  Task StopAsync();
}
