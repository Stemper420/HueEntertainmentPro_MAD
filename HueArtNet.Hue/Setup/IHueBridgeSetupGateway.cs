namespace HueArtNet.Hue.Setup;

public interface IHueBridgeSetupGateway
{
  Task<IReadOnlyList<HueBridgeCandidate>> DiscoverAsync(TimeSpan timeout, CancellationToken cancellationToken);

  Task<HueBridgePairingResult> PairAsync(
    HueBridgeCandidate bridge,
    string applicationName,
    string deviceName,
    CancellationToken cancellationToken);
}
