namespace HueEntertainmentPro.Database.Models
{
  public class ProArea
  {
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public bool ArtNetEnabled { get; set; }
    public string? ArtNetBindAddress { get; set; }
    public string ArtNetTimeoutMode { get; set; } = "Blackout";
    //public bool IsAlwaysVisible { get; set; }
    //public bool HideDisconnect { get; set; }

    public IList<ProAreaBridgeGroup> ProAreaBridgeGroups { get; set; } = new List<ProAreaBridgeGroup>();

    public DateTime CreatedDate { get; set; }
  }
}
