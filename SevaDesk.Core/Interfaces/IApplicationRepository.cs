using SevaDesk.Core.Models;

namespace SevaDesk.Core.Interfaces;

public interface IApplicationRepository
{
    Task<IEnumerable<ApplicationItem>> GetAllAsync(string? status = null);
    Task<IEnumerable<ApplicationItem>> GetByCustomerIdAsync(string customerId);
    Task<ApplicationItem?> GetByIdAsync(string id);
    Task<ApplicationItem> CreateAsync(ApplicationItem app);
    Task UpdateAsync(ApplicationItem app);
    Task DeleteAsync(string id);

    Task<IEnumerable<ApplicationTemplate>> GetAllTemplatesAsync(string? category = null);
    Task<ApplicationTemplate?> GetTemplateByIdAsync(string id);
    Task<IEnumerable<ApplicationTemplate>> SearchTemplatesAsync(string query);
    Task<ApplicationTemplate> CreateTemplateAsync(ApplicationTemplate template);
    Task UpdateTemplateAsync(ApplicationTemplate template);
    Task DeleteTemplateAsync(string id);
}
