using SevaDesk.Core.Models;

namespace SevaDesk.Core.Interfaces;

public interface IResourceRepository
{
    Task<IEnumerable<ResourceItem>> GetAllAsync();
    Task ToggleFavoriteAsync(string id, bool isFavorite);
    Task<ResourceItem> AddCustomResourceAsync(ResourceItem item);
    Task UpdateResourceAsync(ResourceItem item);
    Task DeleteAsync(string id);
}
