using HueArtNet.Core.Profiles;
using HueArtNet.Hue.Runtime;

namespace HueArtNet.WinUI.Presentation;

internal static class DashboardStatusComposer
{
  public static DashboardStatusSnapshot Compose(
    IReadOnlyList<ShowProfile> profiles,
    ArtNetShowRuntimeStatus runtimeStatus,
    ShowProfile? editorProfile,
    IReadOnlyList<string> validationErrors,
    string? message,
    string? pairingBridgeName,
    int? pairingRowIndex,
    string? selectedBridgeDisplayName,
    bool hasPendingRuntimeChanges)
  {
    var editorValidation = editorProfile == null
      ? null
      : ShowProfileValidator.Validate(editorProfile);

    return new DashboardStatusSnapshot(
      RuntimeState: GetRuntimeState(runtimeStatus),
      RuntimeProfile: GetRuntimeProfile(profiles, runtimeStatus, hasPendingRuntimeChanges),
      ArtNetState: runtimeStatus.IsRunning ? "Listening" : "Idle",
      ArtNetPackets: GetArtNetDetails(runtimeStatus),
      HueState: GetHueState(runtimeStatus),
      HueSessions: GetHueSessions(runtimeStatus),
      ValidationState: IsValid(profiles, editorValidation, validationErrors) ? "Valid" : "Invalid",
      ValidationDetails: GetValidationDetails(editorValidation, validationErrors),
      BridgeSetupStatus: GetBridgeSetupStatus(pairingBridgeName, pairingRowIndex, selectedBridgeDisplayName),
      ActivityLog: BuildActivityLog(profiles, runtimeStatus, message, validationErrors));
  }

  private static string GetRuntimeState(ArtNetShowRuntimeStatus runtimeStatus)
  {
    if (!runtimeStatus.IsRunning)
      return "Stopped";

    return runtimeStatus.IsTimedOut ? "Timed out" : "Running";
  }

  private static string GetRuntimeProfile(
    IReadOnlyList<ShowProfile> profiles,
    ArtNetShowRuntimeStatus runtimeStatus,
    bool hasPendingRuntimeChanges)
  {
    if (!runtimeStatus.IsRunning)
      return $"{profiles.Count} stored profile(s)";

    var profileName = runtimeStatus.ProfileName ?? "Unnamed profile";
    return hasPendingRuntimeChanges ? $"{profileName} | restart required" : profileName;
  }

  private static string GetArtNetDetails(ArtNetShowRuntimeStatus runtimeStatus)
  {
    return runtimeStatus.IsRunning
      ? $"{runtimeStatus.BindAddress ?? "0.0.0.0"}:{runtimeStatus.Port} | {runtimeStatus.ArtNet.PacketsPerSecond:F1}/s | universes {FormatUniverses(runtimeStatus.ArtNet.ActiveUniverses)}"
      : "UDP input stopped";
  }

  private static string GetHueState(ArtNetShowRuntimeStatus runtimeStatus)
  {
    if (!runtimeStatus.HueOutput.IsRunning)
      return "Offline";

    int connectedSessions = runtimeStatus.HueOutput.Sessions.Count(x => x.IsConnected);
    return $"{connectedSessions}/{runtimeStatus.HueOutput.Sessions.Count} online";
  }

  private static string GetHueSessions(ArtNetShowRuntimeStatus runtimeStatus)
  {
    return runtimeStatus.HueOutput.Sessions.Count == 0
      ? "No active Hue sessions"
      : string.Join(", ", runtimeStatus.HueOutput.Sessions.Select(x => x.Name));
  }

  private static bool IsValid(
    IReadOnlyList<ShowProfile> profiles,
    ProfileValidationResult? editorValidation,
    IReadOnlyList<string> validationErrors)
  {
    return validationErrors.Count == 0 && (editorValidation?.IsValid ?? profiles.Any());
  }

  private static string GetValidationDetails(
    ProfileValidationResult? editorValidation,
    IReadOnlyList<string> validationErrors)
  {
    if (validationErrors.Count > 0)
      return validationErrors[0];

    if (editorValidation == null)
      return "Create or load a profile";

    return editorValidation.IsValid
      ? $"Universes {FormatUniverses(editorValidation.ActiveUniverses)}"
      : editorValidation.Errors.FirstOrDefault() ?? "Profile has validation errors";
  }

  private static string GetBridgeSetupStatus(
    string? pairingBridgeName,
    int? pairingRowIndex,
    string? selectedBridgeDisplayName)
  {
    if (!string.IsNullOrWhiteSpace(pairingBridgeName))
      return $"Paired {pairingBridgeName} for hub {(pairingRowIndex ?? 0) + 1}";

    return !string.IsNullOrWhiteSpace(selectedBridgeDisplayName)
      ? $"Selected {selectedBridgeDisplayName}"
      : "No bridge selected";
  }

  private static string BuildActivityLog(
    IReadOnlyList<ShowProfile> profiles,
    ArtNetShowRuntimeStatus runtimeStatus,
    string? message,
    IReadOnlyList<string> validationErrors)
  {
    var lines = new List<string>();

    if (!string.IsNullOrWhiteSpace(message))
      lines.Add(message);

    foreach (var error in validationErrors)
      lines.Add($"- {error}");

    lines.Add($"Stored profiles: {profiles.Count}");
    foreach (var profile in profiles)
    {
      var validation = ShowProfileValidator.Validate(profile);
      lines.Add($"- {profile.Name}: {(validation.IsValid ? "valid" : "invalid")} / universes {string.Join(", ", validation.ActiveUniverses)}");
      foreach (var error in validation.Errors)
        lines.Add($"  {error}");
    }

    lines.Add($"Runtime running: {runtimeStatus.IsRunning}");
    if (runtimeStatus.IsRunning)
    {
      lines.Add($"Profile: {runtimeStatus.ProfileName}");
      lines.Add($"Art-Net: {runtimeStatus.BindAddress ?? "0.0.0.0"}:{runtimeStatus.Port} / universes {string.Join(", ", runtimeStatus.ArtNet.ActiveUniverses)}");
      lines.Add($"Packets: {runtimeStatus.ArtNet.AcceptedPackets} accepted, {runtimeStatus.ArtNet.IgnoredPackets} ignored, {runtimeStatus.ArtNet.PacketsPerSecond:F1}/s");
      lines.Add($"Timeout: {(runtimeStatus.IsTimedOut ? "yes" : "no")}");
      if (!string.IsNullOrWhiteSpace(runtimeStatus.LastError))
        lines.Add($"Runtime error: {runtimeStatus.LastError}");
    }

    lines.Add($"Hue output running: {runtimeStatus.HueOutput.IsRunning}");
    foreach (var session in runtimeStatus.HueOutput.Sessions)
      lines.Add($"- {session.Name} ({session.BridgeIp}): {(session.IsConnected ? "connected" : "offline")} {session.LastError}");

    return string.Join(Environment.NewLine, lines);
  }

  private static string FormatUniverses(IReadOnlyCollection<int> universes)
  {
    return universes.Count == 0 ? "none" : string.Join(", ", universes);
  }
}
