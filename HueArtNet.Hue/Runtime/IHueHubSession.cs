namespace HueArtNet.Hue.Runtime;

public interface IHueHubSession
{
  HueHubSessionStatus Status { get; }
  IReadOnlyCollection<int> AvailableLightIds { get; }
  Task ConnectAsync(CancellationToken cancellationToken);
  Task ApplyOutputsAsync(IReadOnlyList<Core.ArtNet.ArtNetLightOutput> outputs, CancellationToken cancellationToken);
  Task StopAsync();
}
