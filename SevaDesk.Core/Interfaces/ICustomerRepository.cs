using SevaDesk.Core.Models;

namespace SevaDesk.Core.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(string id);
    Task<IEnumerable<Customer>> SearchAsync(string query);
    Task<Customer> CreateAsync(Customer customer);
    Task UpdateAsync(Customer customer);
    Task UpdatePhotoAsync(string customerId, string? photoPath);
    Task<string> GenerateNextCodeAsync();
}
