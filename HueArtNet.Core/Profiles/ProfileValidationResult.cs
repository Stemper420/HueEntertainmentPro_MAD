namespace HueArtNet.Core.Profiles;

public sealed class ProfileValidationResult
{
  public bool IsValid => Errors.Count == 0;
  public List<string> Errors { get; init; } = new();
  public IReadOnlyList<int> ActiveUniverses { get; init; } = Array.Empty<int>();
}
