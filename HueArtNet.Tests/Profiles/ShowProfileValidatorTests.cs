using HueArtNet.Core.Profiles;

namespace HueArtNet.Tests.Profiles;

public class ShowProfileValidatorTests
{
  [Fact]
  public void Validate_accepts_profile_with_four_enabled_hubs()
  {
    var profile = new ShowProfile
    {
      Name = "Main room",
      HubMappings = Enumerable.Range(0, 4)
        .Select(index => CreateMapping(index, universe: index, startChannel: 1, lightCount: 10))
        .ToList()
    };

    var result = ShowProfileValidator.Validate(profile);

    Assert.True(result.IsValid);
    Assert.Empty(result.Errors);
    Assert.Equal(new[] { 0, 1, 2, 3 }, result.ActiveUniverses);
  }

  [Fact]
  public void Validate_rejects_more_than_four_enabled_hubs()
  {
    var profile = new ShowProfile
    {
      Name = "Too many",
      HubMappings = Enumerable.Range(0, 5)
        .Select(index => CreateMapping(index, universe: index, startChannel: 1, lightCount: 1))
        .ToList()
    };

    var result = ShowProfileValidator.Validate(profile);

    Assert.False(result.IsValid);
    Assert.Contains("A profile can enable at most 4 Hue hubs.", result.Errors);
  }

  [Fact]
  public void Validate_rejects_dmx_blocks_that_exceed_channel_512()
  {
    var profile = new ShowProfile
    {
      Name = "Overflow",
      HubMappings = new List<HubMapping>
      {
        CreateMapping(0, universe: 0, startChannel: 511, lightCount: 1, FixtureMode.Rgb3)
      }
    };

    var result = ShowProfileValidator.Validate(profile);

    Assert.False(result.IsValid);
    Assert.Contains(result.Errors, x => x.Contains("exceeds channel 512", StringComparison.OrdinalIgnoreCase));
  }

  [Fact]
  public void Validate_rejects_overlapping_blocks_in_same_universe()
  {
    var profile = new ShowProfile
    {
      Name = "Overlap",
      HubMappings = new List<HubMapping>
      {
        CreateMapping(0, universe: 0, startChannel: 1, lightCount: 2, FixtureMode.Rgb3),
        CreateMapping(1, universe: 0, startChannel: 4, lightCount: 1, FixtureMode.Rgb3)
      }
    };

    var result = ShowProfileValidator.Validate(profile);

    Assert.False(result.IsValid);
    Assert.Contains(result.Errors, x => x.Contains("overlaps", StringComparison.OrdinalIgnoreCase));
  }

  [Fact]
  public void Validate_rejects_enabled_hub_without_hue_streaming_credentials()
  {
    var profile = new ShowProfile
    {
      Name = "Missing credentials",
      HubMappings = new List<HubMapping>
      {
        CreateMapping(0, universe: 0, startChannel: 1, lightCount: 1, withCredentials: false)
      }
    };

    var result = ShowProfileValidator.Validate(profile);

    Assert.False(result.IsValid);
    Assert.Contains(result.Errors, x => x.Contains("application key", StringComparison.OrdinalIgnoreCase));
    Assert.Contains(result.Errors, x => x.Contains("entertainment key", StringComparison.OrdinalIgnoreCase));
  }

  [Fact]
  public void Validate_rejects_invalid_artnet_bind_address()
  {
    var profile = new ShowProfile
    {
      Name = "Bad bind",
      ArtNetInput = new ArtNetInputConfig
      {
        BindAddress = "not-an-ip"
      },
      HubMappings = new List<HubMapping>
      {
        CreateMapping(0, universe: 0, startChannel: 1, lightCount: 1)
      }
    };

    var result = ShowProfileValidator.Validate(profile);

    Assert.False(result.IsValid);
    Assert.Contains(result.Errors, x => x.Contains("bind", StringComparison.OrdinalIgnoreCase));
  }

  private static HubMapping CreateMapping(int index, int universe, int startChannel, int lightCount, FixtureMode fixtureMode = FixtureMode.Rgb3, bool withCredentials = true)
  {
    return new HubMapping
    {
      Name = $"Hub {index + 1}",
      BridgeId = Guid.NewGuid(),
      BridgeIp = $"192.168.1.{20 + index}",
      ApplicationKey = withCredentials ? $"app-key-{index}" : string.Empty,
      EntertainmentKey = withCredentials ? $"ent-key-{index}" : string.Empty,
      EntertainmentGroupId = Guid.NewGuid(),
      Universe = universe,
      StartChannel = startChannel,
      FixtureMode = fixtureMode,
      LightOrder = Enumerable.Range(1, lightCount).ToList(),
      Enabled = true
    };
  }
}
