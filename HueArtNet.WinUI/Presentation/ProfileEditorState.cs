namespace HueArtNet.WinUI.Presentation;

internal sealed record ProfileEditorState(
  string Name,
  string BindAddress,
  string ArtNetPort,
  string OutputFps,
  string TimeoutSeconds,
  string TimeoutModeTag,
  IReadOnlyList<HubEditorState> Hubs);
