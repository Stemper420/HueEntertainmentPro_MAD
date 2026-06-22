namespace HueArtNet.Hue.Setup;

public sealed record HueBridgePairingResult(
  string BridgeId,
  string BridgeIp,
  string BridgeName,
  string ApplicationKey,
  string EntertainmentKey,
  IReadOnlyList<HueEntertainmentGroupInfo> EntertainmentGroups);
