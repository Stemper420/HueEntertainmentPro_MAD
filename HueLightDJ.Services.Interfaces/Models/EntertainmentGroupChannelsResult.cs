using ProtoBuf;
using System.Collections.Generic;

namespace HueLightDJ.Services.Interfaces.Models
{
  [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
  public class EntertainmentGroupChannelsResult
  {
    public IEnumerable<EntertainmentChannelInfo> Channels { get; set; } = new List<EntertainmentChannelInfo>();
    public string? ErrorMessage { get; set; }
  }
}
