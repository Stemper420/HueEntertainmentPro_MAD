namespace HueArtNet.Hue.Setup;

public interface IHueBridgeSetupService
{
  Task<IReadOnlyList<HueBridgeCandidate>> DiscoverAsync(TimeSpan timeout, CancellationToken cancellationToken);

  Task<HueBridgePairingResult> PairAsync(
    HueBridgeCandidate bridge,
    string applicationName,
    string deviceName,
    CancellationToken cancellationToken);
}
