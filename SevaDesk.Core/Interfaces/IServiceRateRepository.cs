using SevaDesk.Core.Models;

namespace SevaDesk.Core.Interfaces;

public interface IServiceRateRepository
{
    Task<IEnumerable<ServiceRateItem>> GetAllActiveRatesAsync();
    Task<IEnumerable<ServiceRateItem>> GetAllRatesAsync();
    Task AddRateAsync(ServiceRateItem rate);
    Task UpdateRateAsync(ServiceRateItem rate);
    Task DeleteRateAsync(string id);
}
