using ProtoBuf;
using System;

namespace HueLightDJ.Services.Interfaces.Models
{
  [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
  public class EntertainmentChannelInfo
  {
    public int ChannelId { get; set; }
    public Guid? EntertainmentId { get; set; }
    public int PositionIndex { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
  }
}
