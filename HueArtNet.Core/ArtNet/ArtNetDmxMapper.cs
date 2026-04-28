using HueArtNet.Core.Profiles;

namespace HueArtNet.Core.ArtNet;

public static class ArtNetDmxMapper
{
  private const double CoolWhiteRed = 0.73;
  private const double CoolWhiteGreen = 1.00;
  private const double CoolWhiteBlue = 0.90;
  private const double WarmWhiteRed = 1.00;
  private const double WarmWhiteGreen = 0.80;
  private const double WarmWhiteBlue = 0.59;

  public static IEnumerable<ArtNetLightOutput> MapFrame(byte[] dmx, HubMapping mapping, IReadOnlyCollection<int> availableLightIds)
  {
    var lightIds = GetOrderedLightIds(mapping, availableLightIds);
    int fixtureWidth = GetChannelWidth(mapping.FixtureMode);
    int offset = mapping.StartChannel - 1;

    foreach (int lightId in lightIds)
    {
      if (offset + fixtureWidth > dmx.Length)
        yield break;

      yield return MapFixture(lightId, dmx.AsSpan(offset, fixtureWidth), mapping.FixtureMode);
      offset += fixtureWidth;
    }
  }

  public static int GetChannelWidth(FixtureMode fixtureMode)
  {
    return fixtureMode switch
    {
      FixtureMode.Rgb3 => 3,
      FixtureMode.Rgbww5 => 5,
      FixtureMode.DimmerRgbww6 => 6,
      _ => 3
    };
  }

  private static ArtNetLightOutput MapFixture(int lightId, ReadOnlySpan<byte> channels, FixtureMode fixtureMode)
  {
    double master = 1d;
    double red;
    double green;
    double blue;

    if (fixtureMode == FixtureMode.DimmerRgbww6)
    {
      master = channels[0] / 255d;
      red = channels[1];
      green = channels[2];
      blue = channels[3];
      AddWhiteMix(channels[4], channels[5], ref red, ref green, ref blue);
    }
    else if (fixtureMode == FixtureMode.Rgbww5)
    {
      red = channels[0];
      green = channels[1];
      blue = channels[2];
      AddWhiteMix(channels[3], channels[4], ref red, ref green, ref blue);
    }
    else
    {
      red = channels[0];
      green = channels[1];
      blue = channels[2];
    }

    double max = Math.Max(red, Math.Max(green, blue));
    if (max <= 0 || master <= 0)
    {
      return new ArtNetLightOutput
      {
        LightId = lightId,
        HexColor = "000000",
        Brightness = 0
      };
    }

    int normalizedRed = ClampToByte((red / max) * 255d);
    int normalizedGreen = ClampToByte((green / max) * 255d);
    int normalizedBlue = ClampToByte((blue / max) * 255d);

    return new ArtNetLightOutput
    {
      LightId = lightId,
      HexColor = $"{normalizedRed:X2}{normalizedGreen:X2}{normalizedBlue:X2}",
      Brightness = Math.Clamp((max / 255d) * master, 0d, 1d)
    };
  }

  private static void AddWhiteMix(byte coolWhite, byte warmWhite, ref double red, ref double green, ref double blue)
  {
    red += (coolWhite * CoolWhiteRed) + (warmWhite * WarmWhiteRed);
    green += (coolWhite * CoolWhiteGreen) + (warmWhite * WarmWhiteGreen);
    blue += (coolWhite * CoolWhiteBlue) + (warmWhite * WarmWhiteBlue);
  }

  private static List<int> GetOrderedLightIds(HubMapping mapping, IReadOnlyCollection<int> availableLightIds)
  {
    var available = availableLightIds.ToHashSet();
    var ordered = mapping.LightOrder
      .Where(available.Contains)
      .Distinct()
      .ToList();

    ordered.AddRange(availableLightIds
      .Where(id => !ordered.Contains(id))
      .OrderBy(id => id));

    return ordered;
  }

  private static int ClampToByte(double value)
  {
    return (int)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0, 255);
  }
}
