namespace HueArtNet.WinUI.Presentation;

internal sealed record HubEditorState(
  bool Enabled,
  string Name,
  string BridgeIp,
  string ApplicationKey,
  string EntertainmentKey,
  string EntertainmentGroupId,
  string Universe,
  string StartChannel,
  string FixtureModeTag,
  string LightOrder);
