namespace HueArtNet.WinUI.Presentation;

internal sealed record DashboardStatusSnapshot(
  string RuntimeState,
  string RuntimeProfile,
  string ArtNetState,
  string ArtNetPackets,
  string HueState,
  string HueSessions,
  string ValidationState,
  string ValidationDetails,
  string BridgeSetupStatus,
  string ActivityLog);
