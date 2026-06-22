using HueArtNet.Core.ArtNet;
using System.Net;
using System.Net.Sockets;

namespace HueArtNet.Tests.Runtime;

public class ArtNetReceiverTests
{
  [Fact]
  public async Task Receiver_accepts_configured_artdmx_universe()
  {
    await using var receiver = new ArtNetReceiver(new ArtNetReceiverOptions
    {
      BindAddress = IPAddress.Loopback,
      Port = 0,
      Universes = new[] { 7 }
    });

    await receiver.StartAsync(CancellationToken.None);

    using var sender = new UdpClient();
    byte[] packet = BuildArtDmxPacket(universe: 7, 10, 20, 30);
    await sender.SendAsync(packet, packet.Length, new IPEndPoint(IPAddress.Loopback, receiver.BoundPort));

    await WaitUntilAsync(() => receiver.FrameBuffer.GetStats(DateTimeOffset.UtcNow).AcceptedPackets == 1);

    var frame = receiver.FrameBuffer.ReadLatestFrames().Single();
    Assert.Equal(7, frame.Universe);
    Assert.Equal(new byte[] { 10, 20, 30 }, frame.Data);
  }

  [Fact]
  public async Task Receiver_ignores_unconfigured_universe()
  {
    await using var receiver = new ArtNetReceiver(new ArtNetReceiverOptions
    {
      BindAddress = IPAddress.Loopback,
      Port = 0,
      Universes = new[] { 1 }
    });

    await receiver.StartAsync(CancellationToken.None);

    using var sender = new UdpClient();
    byte[] packet = BuildArtDmxPacket(universe: 2, 255);
    await sender.SendAsync(packet, packet.Length, new IPEndPoint(IPAddress.Loopback, receiver.BoundPort));

    await WaitUntilAsync(() => receiver.FrameBuffer.GetStats(DateTimeOffset.UtcNow).IgnoredPackets == 1);

    Assert.Empty(receiver.FrameBuffer.ReadLatestFrames());
  }

  private static async Task WaitUntilAsync(Func<bool> condition)
  {
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
    while (!condition())
    {
      cts.Token.ThrowIfCancellationRequested();
      await Task.Delay(25, cts.Token);
    }
  }

  private static byte[] BuildArtDmxPacket(int universe, params byte[] dmx)
  {
    byte[] packet = new byte[18 + dmx.Length];
    byte[] header = "Art-Net\0"u8.ToArray();
    Buffer.BlockCopy(header, 0, packet, 0, header.Length);
    packet[8] = 0x00;
    packet[9] = 0x50;
    packet[10] = 0x00;
    packet[11] = 14;
    packet[12] = 1;
    packet[13] = 0;
    packet[14] = (byte)(universe & 0xFF);
    packet[15] = (byte)((universe >> 8) & 0xFF);
    packet[16] = (byte)((dmx.Length >> 8) & 0xFF);
    packet[17] = (byte)(dmx.Length & 0xFF);
    Buffer.BlockCopy(dmx, 0, packet, 18, dmx.Length);
    return packet;
  }
}
