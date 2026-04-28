using HueLightDJ.Services.ArtNet;

namespace HueLightDJ.Services.Tests.ArtNet;

public class ArtDmxPacketParserTests
{
  [Fact]
  public void TryParse_returns_frame_for_valid_artdmx_packet()
  {
    byte[] packet = BuildArtDmxPacket(universe: 17, sequence: 9, physical: 2, 1, 2, 3, 4);

    bool parsed = ArtDmxPacketParser.TryParse(packet, out ArtDmxFrame? frame, out string? error);

    Assert.True(parsed);
    Assert.Null(error);
    Assert.NotNull(frame);
    Assert.Equal(17, frame.Universe);
    Assert.Equal(9, frame.Sequence);
    Assert.Equal(2, frame.Physical);
    Assert.Equal(new byte[] { 1, 2, 3, 4 }, frame.Data);
  }

  [Fact]
  public void TryParse_rejects_packets_without_artnet_header()
  {
    byte[] packet = BuildArtDmxPacket(universe: 0, sequence: 0, physical: 0, 1, 2, 3);
    packet[0] = (byte)'X';

    bool parsed = ArtDmxPacketParser.TryParse(packet, out ArtDmxFrame? frame, out string? error);

    Assert.False(parsed);
    Assert.Null(frame);
    Assert.Equal("Invalid Art-Net header.", error);
  }

  [Fact]
  public void TryParse_rejects_non_artdmx_opcode()
  {
    byte[] packet = BuildArtDmxPacket(universe: 0, sequence: 0, physical: 0, 1, 2, 3);
    packet[8] = 0x10;
    packet[9] = 0x21;

    bool parsed = ArtDmxPacketParser.TryParse(packet, out ArtDmxFrame? frame, out string? error);

    Assert.False(parsed);
    Assert.Null(frame);
    Assert.Equal("Unsupported Art-Net opcode.", error);
  }

  [Fact]
  public void TryParse_rejects_short_dmx_payload()
  {
    byte[] packet = BuildArtDmxPacket(universe: 0, sequence: 0, physical: 0, 1, 2, 3);
    packet[16] = 0;
    packet[17] = 10;

    bool parsed = ArtDmxPacketParser.TryParse(packet, out ArtDmxFrame? frame, out string? error);

    Assert.False(parsed);
    Assert.Null(frame);
    Assert.Equal("ArtDMX payload length exceeds packet size.", error);
  }

  private static byte[] BuildArtDmxPacket(int universe, byte sequence, byte physical, params byte[] dmx)
  {
    byte[] packet = new byte[18 + dmx.Length];
    byte[] header = "Art-Net\0"u8.ToArray();
    Buffer.BlockCopy(header, 0, packet, 0, header.Length);
    packet[8] = 0x00;
    packet[9] = 0x50;
    packet[10] = 0x00;
    packet[11] = 14;
    packet[12] = sequence;
    packet[13] = physical;
    packet[14] = (byte)(universe & 0xFF);
    packet[15] = (byte)((universe >> 8) & 0xFF);
    packet[16] = (byte)((dmx.Length >> 8) & 0xFF);
    packet[17] = (byte)(dmx.Length & 0xFF);
    Buffer.BlockCopy(dmx, 0, packet, 18, dmx.Length);
    return packet;
  }
}
