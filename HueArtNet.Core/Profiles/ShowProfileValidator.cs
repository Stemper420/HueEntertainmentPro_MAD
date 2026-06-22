using System.Net;
using System.Net.Sockets;
using HueArtNet.Core.ArtNet;

namespace HueArtNet.Core.Profiles;

public static class ShowProfileValidator
{
  public static ProfileValidationResult Validate(ShowProfile profile)
  {
    var errors = new List<string>();
    var enabledMappings = profile.HubMappings.Where(x => x.Enabled).ToList();

    if (string.IsNullOrWhiteSpace(profile.Name))
      errors.Add("Profile name is required.");

    if (enabledMappings.Count > 4)
      errors.Add("A profile can enable at most 4 Hue hubs.");

    if (profile.ArtNetInput.Port is < 1 or > 65535)
      errors.Add("Art-Net UDP port must be between 1 and 65535.");

    if (!string.IsNullOrWhiteSpace(profile.ArtNetInput.BindAddress)
      && (!IPAddress.TryParse(profile.ArtNetInput.BindAddress, out var bindAddress)
        || bindAddress.AddressFamily != AddressFamily.InterNetwork))
    {
      errors.Add("Art-Net bind address must be a valid IPv4 address.");
    }

    if (profile.Output.FramesPerSecond is < 1 or > 60)
      errors.Add("Output FPS must be between 1 and 60.");

    foreach (var mapping in enabledMappings)
      ValidateMapping(mapping, errors);

    ValidateUniverseOverlaps(enabledMappings, errors);

    return new ProfileValidationResult
    {
      Errors = errors,
      ActiveUniverses = enabledMappings.Select(x => x.Universe).Distinct().OrderBy(x => x).ToList()
    };
  }

  private static void ValidateMapping(HubMapping mapping, List<string> errors)
  {
    string name = string.IsNullOrWhiteSpace(mapping.Name) ? mapping.BridgeIp : mapping.Name;

    if (mapping.BridgeId == Guid.Empty)
      errors.Add($"{name}: BridgeId is required.");

    if (string.IsNullOrWhiteSpace(mapping.BridgeIp))
      errors.Add($"{name}: Bridge IP is required.");

    if (string.IsNullOrWhiteSpace(mapping.ApplicationKey))
      errors.Add($"{name}: Hue application key is required.");

    if (string.IsNullOrWhiteSpace(mapping.EntertainmentKey))
      errors.Add($"{name}: Hue entertainment key is required.");

    if (mapping.EntertainmentGroupId == Guid.Empty)
      errors.Add($"{name}: EntertainmentGroupId is required.");

    if (mapping.Universe is < 0 or > 32767)
      errors.Add($"{name}: Universe must be between 0 and 32767.");

    if (mapping.StartChannel is < 1 or > 512)
      errors.Add($"{name}: Start channel must be between 1 and 512.");

    int lightCount = Math.Max(mapping.LightOrder.Distinct().Count(), 1);
    int lastChannel = GetLastChannel(mapping.StartChannel, mapping.FixtureMode, lightCount);
    if (lastChannel > 512)
      errors.Add($"{name}: DMX block {mapping.StartChannel}-{lastChannel} exceeds channel 512.");
  }

  private static void ValidateUniverseOverlaps(IReadOnlyCollection<HubMapping> mappings, List<string> errors)
  {
    var ranges = mappings
      .Select(mapping => new DmxRange(
        Mapping: mapping,
        First: mapping.StartChannel,
        Last: GetLastChannel(mapping.StartChannel, mapping.FixtureMode, Math.Max(mapping.LightOrder.Distinct().Count(), 1))))
      .Where(x => x.Last <= 512)
      .GroupBy(x => x.Mapping.Universe);

    foreach (var universe in ranges)
    {
      var ordered = universe.OrderBy(x => x.First).ToList();
      for (int index = 1; index < ordered.Count; index++)
      {
        var previous = ordered[index - 1];
        var current = ordered[index];
        if (current.First <= previous.Last)
          errors.Add($"{current.Mapping.Name}: DMX block {current.First}-{current.Last} overlaps {previous.Mapping.Name} on universe {universe.Key}.");
      }
    }
  }

  private static int GetLastChannel(int startChannel, FixtureMode fixtureMode, int lightCount)
  {
    return startChannel + (ArtNetDmxMapper.GetChannelWidth(fixtureMode) * lightCount) - 1;
  }

  private sealed record DmxRange(HubMapping Mapping, int First, int Last);
}
