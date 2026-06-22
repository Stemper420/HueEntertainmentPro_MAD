using ProtoBuf;
using System;

namespace HueLightDJ.Services.Interfaces.Models.Requests
{
  [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
  public class IdentifyEntertainmentChannelRequest
  {
    public required string Ip { get; set; }
    public string? Key { get; set; }
    public Guid GroupId { get; set; }
    public int ChannelId { get; set; }
  }
}
