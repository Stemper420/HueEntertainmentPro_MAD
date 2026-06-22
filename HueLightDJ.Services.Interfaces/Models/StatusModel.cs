using ProtoBuf;
using System.Collections.Generic;

namespace HueLightDJ.Services.Interfaces.Models
{
  [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
  public class StatusModel
  {
    public int Bpm { get; set; }
    public bool IsAutoMode { get; set; }
    public bool AutoModeHasRandomEffects { get; set; }

    public bool ShowDisconnect { get; set; }
    public GroupConfiguration? CurrentGroup { get; set; }
    public ArtNetStatusModel ArtNet { get; set; } = new();

    public List<GroupInfoViewModel> Groups { get; set; } = new();

    //public bool IsConnected => !string.IsNullOrEmpty(CurrentGroup);
  }

  [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
  public class GroupInfoViewModel
  {
    public required string Name { get; set; }

  }
}
