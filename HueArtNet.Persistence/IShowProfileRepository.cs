using HueArtNet.Core.Profiles;

namespace HueArtNet.Persistence;

public interface IShowProfileRepository
{
  Task<IReadOnlyList<ShowProfile>> GetAllAsync(CancellationToken cancellationToken);
  Task<ShowProfile?> GetAsync(Guid id, CancellationToken cancellationToken);
  Task SaveAsync(ShowProfile profile, CancellationToken cancellationToken);
  Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
