using HueArtNet.Core.ArtNet;

namespace HueArtNet.Tests.Runtime;

public class ArtNetFrameBufferTests
{
  [Fact]
  public void AddFrame_keeps_latest_frame_per_configured_universe()
  {
    var buffer = new ArtNetFrameBuffer(new[] { 1, 2 });

    Assert.True(buffer.AddFrame(new ArtDmxFrame { Universe = 1, Sequence = 1, Physical = 0, Data = new byte[] { 10 } }, DateTimeOffset.UtcNow));
    Assert.True(buffer.AddFrame(new ArtDmxFrame { Universe = 1, Sequence = 2, Physical = 0, Data = new byte[] { 20 } }, DateTimeOffset.UtcNow));
    Assert.True(buffer.AddFrame(new ArtDmxFrame { Universe = 2, Sequence = 1, Physical = 0, Data = new byte[] { 30 } }, DateTimeOffset.UtcNow));

    var frames = buffer.ReadLatestFrames().OrderBy(x => x.Universe).ToList();
    var stats = buffer.GetStats(DateTimeOffset.UtcNow);

    Assert.Equal(2, frames.Count);
    Assert.Equal(new byte[] { 20 }, frames[0].Data);
    Assert.Equal(new byte[] { 30 }, frames[1].Data);
    Assert.Equal(3, stats.AcceptedPackets);
    Assert.Equal(0, stats.IgnoredPackets);
    Assert.Equal(new[] { 1, 2 }, stats.ActiveUniverses);
  }

  [Fact]
  public void AddFrame_ignores_unconfigured_universe()
  {
    var buffer = new ArtNetFrameBuffer(new[] { 7 });

    bool accepted = buffer.AddFrame(new ArtDmxFrame { Universe = 8, Sequence = 1, Physical = 0, Data = new byte[] { 255 } }, DateTimeOffset.UtcNow);

    Assert.False(accepted);
    Assert.Empty(buffer.ReadLatestFrames());
    Assert.Equal(1, buffer.GetStats(DateTimeOffset.UtcNow).IgnoredPackets);
  }
}
