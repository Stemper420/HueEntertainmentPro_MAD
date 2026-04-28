using System.Globalization;
using HueArtNet.Core.Profiles;

namespace HueArtNet.WinUI.Presentation;

internal static class ProfileEditorMapper
{
  public static ShowProfile BuildProfile(ShowProfile? source, ProfileEditorState state)
  {
    var baseProfile = source ?? CreateTemplate();
    return new ShowProfile
    {
      Id = baseProfile.Id,
      Name = state.Name.Trim(),
      IsDefault = true,
      ArtNetInput = new ArtNetInputConfig
      {
        BindAddress = NormalizeBindAddress(state.BindAddress),
        Port = ParseInt(state.ArtNetPort, 6454)
      },
      Output = new OutputConfig
      {
        FramesPerSecond = ParseInt(state.OutputFps, 20),
        BrightnessLimit = baseProfile.Output.BrightnessLimit
      },
      FailSafe = new FailSafeConfig
      {
        Mode = ParseFailSafeMode(state.TimeoutModeTag),
        Timeout = TimeSpan.FromSeconds(Math.Max(ParseDouble(state.TimeoutSeconds, 2), 0.1))
      },
      HubMappings = state.Hubs
        .Select((hub, index) => BuildMapping(baseProfile.HubMappings.ElementAtOrDefault(index), hub, index))
        .ToList()
    };
  }

  public static ProfileEditorState FromProfile(ShowProfile profile)
  {
    return new ProfileEditorState(
      Name: profile.Name,
      BindAddress: profile.ArtNetInput.BindAddress ?? string.Empty,
      ArtNetPort: profile.ArtNetInput.Port.ToString(CultureInfo.InvariantCulture),
      OutputFps: profile.Output.FramesPerSecond.ToString(CultureInfo.InvariantCulture),
      TimeoutSeconds: profile.FailSafe.Timeout.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture),
      TimeoutModeTag: profile.FailSafe.Mode.ToString(),
      Hubs: Enumerable.Range(0, 4)
        .Select(index => FromMapping(profile.HubMappings.ElementAtOrDefault(index) ?? CreateTemplateMapping(index)))
        .ToList());
  }

  public static ShowProfile CreateTemplate()
  {
    return new ShowProfile
    {
      Name = "Four hub Art-Net profile",
      IsDefault = true,
      ArtNetInput = new ArtNetInputConfig
      {
        BindAddress = "0.0.0.0",
        Port = 6454
      },
      Output = new OutputConfig
      {
        FramesPerSecond = 20,
        BrightnessLimit = 1
      },
      FailSafe = new FailSafeConfig
      {
        Mode = FailSafeMode.HoldLastFrame,
        Timeout = TimeSpan.FromSeconds(2)
      },
      HubMappings = Enumerable.Range(0, 4).Select(CreateTemplateMapping).ToList()
    };
  }

  private static HubMapping BuildMapping(HubMapping? source, HubEditorState state, int index)
  {
    return new HubMapping
    {
      Id = source?.Id ?? Guid.NewGuid(),
      Name = string.IsNullOrWhiteSpace(state.Name) ? $"Hue hub {index + 1}" : state.Name.Trim(),
      BridgeId = source == null || source.BridgeId == Guid.Empty ? Guid.NewGuid() : source.BridgeId,
      HueBridgeId = source?.HueBridgeId ?? string.Empty,
      BridgeIp = state.BridgeIp.Trim(),
      ApplicationKey = state.ApplicationKey.Trim(),
      EntertainmentKey = state.EntertainmentKey.Trim(),
      EntertainmentGroupId = Guid.TryParse(state.EntertainmentGroupId.Trim(), out var groupId) ? groupId : Guid.Empty,
      Universe = ParseInt(state.Universe, index),
      StartChannel = ParseInt(state.StartChannel, 1),
      FixtureMode = ParseFixtureMode(state.FixtureModeTag),
      LightOrder = ParseLightOrder(state.LightOrder),
      Enabled = state.Enabled,
      Required = source?.Required ?? false
    };
  }

  private static HubEditorState FromMapping(HubMapping mapping)
  {
    return new HubEditorState(
      Enabled: mapping.Enabled,
      Name: mapping.Name,
      BridgeIp: mapping.BridgeIp,
      ApplicationKey: mapping.ApplicationKey,
      EntertainmentKey: mapping.EntertainmentKey,
      EntertainmentGroupId: mapping.EntertainmentGroupId == Guid.Empty ? string.Empty : mapping.EntertainmentGroupId.ToString(),
      Universe: mapping.Universe.ToString(CultureInfo.InvariantCulture),
      StartChannel: mapping.StartChannel.ToString(CultureInfo.InvariantCulture),
      FixtureModeTag: mapping.FixtureMode.ToString(),
      LightOrder: string.Join(",", mapping.LightOrder));
  }

  private static HubMapping CreateTemplateMapping(int index)
  {
    return new HubMapping
    {
      Name = $"Hue hub {index + 1}",
      BridgeId = Guid.NewGuid(),
      HueBridgeId = string.Empty,
      BridgeIp = $"192.168.1.{20 + index}",
      Universe = index,
      StartChannel = 1,
      FixtureMode = FixtureMode.Rgb3,
      LightOrder = Enumerable.Range(1, 10).ToList(),
      Enabled = true
    };
  }

  private static int ParseInt(string value, int fallback)
  {
    return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
      ? parsed
      : fallback;
  }

  private static double ParseDouble(string value, double fallback)
  {
    return double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
      ? parsed
      : fallback;
  }

  private static List<int> ParseLightOrder(string value)
  {
    var parsed = value
      .Split(new[] { ',', ';', ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
      .Select(item => int.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out int lightId) ? lightId : 0)
      .Where(lightId => lightId > 0)
      .Distinct()
      .ToList();

    return parsed.Count > 0 ? parsed : Enumerable.Range(1, 10).ToList();
  }

  private static string? NormalizeBindAddress(string value)
  {
    return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
  }

  private static FixtureMode ParseFixtureMode(string? tag)
  {
    return Enum.TryParse<FixtureMode>(tag, out var mode) ? mode : FixtureMode.Rgb3;
  }

  private static FailSafeMode ParseFailSafeMode(string? tag)
  {
    return Enum.TryParse<FailSafeMode>(tag, out var mode) ? mode : FailSafeMode.HoldLastFrame;
  }
}
