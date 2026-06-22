using System;

namespace HueLightDJ.Services.ArtNet
{
  public static class ArtDmxPacketParser
  {
    private const int HeaderLength = 18;
    private static readonly byte[] Header = "Art-Net\0"u8.ToArray();

    public static bool TryParse(ReadOnlySpan<byte> packet, out ArtDmxFrame? frame, out string? error)
    {
      frame = null;
      error = null;

      if (packet.Length < HeaderLength)
      {
        error = "ArtDMX packet is too short.";
        return false;
      }

      if (!packet.Slice(0, Header.Length).SequenceEqual(Header))
      {
        error = "Invalid Art-Net header.";
        return false;
      }

      ushort opcode = (ushort)(packet[8] | (packet[9] << 8));
      if (opcode != 0x5000)
      {
        error = "Unsupported Art-Net opcode.";
        return false;
      }

      ushort protocolVersion = (ushort)((packet[10] << 8) | packet[11]);
      if (protocolVersion < 14)
      {
        error = "Unsupported Art-Net protocol version.";
        return false;
      }

      int length = (packet[16] << 8) | packet[17];
      if (length < 0 || length > 512)
      {
        error = "Invalid ArtDMX payload length.";
        return false;
      }

      if (HeaderLength + length > packet.Length)
      {
        error = "ArtDMX payload length exceeds packet size.";
        return false;
      }

      frame = new ArtDmxFrame
      {
        Sequence = packet[12],
        Physical = packet[13],
        Universe = packet[14] | (packet[15] << 8),
        Data = packet.Slice(HeaderLength, length).ToArray()
      };

      return true;
    }
  }
}
