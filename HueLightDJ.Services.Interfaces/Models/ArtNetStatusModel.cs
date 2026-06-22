using ProtoBuf;
using System;
using System.Collections.Generic;

namespace HueLightDJ.Services.Interfaces.Models
{
  [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
  public class ArtNetStatusModel
  {
    public bool IsEnabled { get; set; }
    public bool IsListening { get; set; }
    public bool IsTimedOut { get; set; }
    public string? BindAddress { get; set; }
    public int Port { get; set; } = 6454;
    public ArtNetTimeoutMode TimeoutMode { get; set; }
    public DateTime? LastPacketAtUtc { get; set; }
    public long PacketsReceived { get; set; }
    public double PacketsPerSecond { get; set; }
    public List<int> ActiveUniverses { get; set; } = new();
    public string? LastError { get; set; }
  }
}
