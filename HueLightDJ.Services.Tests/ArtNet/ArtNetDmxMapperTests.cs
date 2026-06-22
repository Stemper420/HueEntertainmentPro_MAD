using HueLightDJ.Services.ArtNet;
using HueLightDJ.Services.Interfaces.Models;

namespace HueLightDJ.Services.Tests.ArtNet;

public class ArtNetDmxMapperTests
{
  [Fact]
  public void MapFrame_maps_rgb_fixtures_from_one_based_start_channel()
  {
    var mapping = new ArtNetBridgeMapping
    {
      Universe = 2,
      StartChannel = 4,
      FixtureMode = ArtNetFixtureMode.Rgb3,
      LightOrder = new List<int> { 7, 8 }
    };

    var result = ArtNetDmxMapper.MapFrame(
      new byte[] { 99, 99, 99, 128, 0, 0, 0, 255, 0 },
      mapping,
      new[] { 7, 8 }).ToList();

    Assert.Equal(2, result.Count);
    Assert.Equal(7, result[0].LightId);
    Assert.Equal("FF0000", result[0].HexColor);
    Assert.Equal(128d / 255d, result[0].Brightness, precision: 4);
    Assert.Equal(8, result[1].LightId);
    Assert.Equal("00FF00", result[1].HexColor);
    Assert.Equal(1d, result[1].Brightness, precision: 4);
  }

  [Fact]
  public void MapFrame_appends_unlisted_lights_after_manual_order()
  {
    var mapping = new ArtNetBridgeMapping
    {
      Universe = 0,
      StartChannel = 1,
      FixtureMode = ArtNetFixtureMode.Rgb3,
      LightOrder = new List<int> { 4 }
    };

    var result = ArtNetDmxMapper.MapFrame(
      new byte[] { 0, 0, 255, 255, 0, 0 },
      mapping,
      new[] { 3, 4 }).ToList();

    Assert.Equal(new[] { 4, 3 }, result.Select(x => x.LightId));
    Assert.Equal("0000FF", result[0].HexColor);
    Assert.Equal("FF0000", result[1].HexColor);
  }

  [Fact]
  public void MapFrame_mixes_rgbww_channels_into_hue_rgb_and_brightness()
  {
    var mapping = new ArtNetBridgeMapping
    {
      Universe = 0,
      StartChannel = 1,
      FixtureMode = ArtNetFixtureMode.Rgbww5,
      LightOrder = new List<int> { 1 }
    };

    var result = ArtNetDmxMapper.MapFrame(
      new byte[] { 0, 0, 0, 255, 128 },
      mapping,
      new[] { 1 }).Single();

    Assert.Equal("E0FFDA", result.HexColor);
    Assert.Equal(1d, result.Brightness, precision: 4);
  }

  [Fact]
  public void MapFrame_applies_six_channel_master_dimmer_to_rgbww_output()
  {
    var mapping = new ArtNetBridgeMapping
    {
      Universe = 0,
      StartChannel = 1,
      FixtureMode = ArtNetFixtureMode.DimmerRgbww6,
      LightOrder = new List<int> { 1 }
    };

    var result = ArtNetDmxMapper.MapFrame(
      new byte[] { 128, 255, 0, 0, 0, 0 },
      mapping,
      new[] { 1 }).Single();

    Assert.Equal("FF0000", result.HexColor);
    Assert.Equal(128d / 255d, result.Brightness, precision: 4);
  }

  [Fact]
  public void ValidateMapping_rejects_fixture_blocks_that_exceed_universe()
  {
    var mapping = new ArtNetBridgeMapping
    {
      Universe = 0,
      StartChannel = 511,
      FixtureMode = ArtNetFixtureMode.Rgb3,
      LightOrder = new List<int> { 1 }
    };

    string? error = ArtNetDmxMapper.ValidateMapping(mapping, lightCount: 1);

    Assert.Equal("DMX block exceeds channel 512.", error);
  }
}
