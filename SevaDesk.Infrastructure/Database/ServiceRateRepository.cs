using Dapper;
using SevaDesk.Core.Interfaces;
using SevaDesk.Core.Models;

namespace SevaDesk.Infrastructure.Database;

public class ServiceRateRepository : IServiceRateRepository
{
    private readonly DatabaseInitializer _dbService;

    public ServiceRateRepository(DatabaseInitializer dbService)
    {
        _dbService = dbService;
    }

    public async Task<IEnumerable<ServiceRateItem>> GetAllActiveRatesAsync()
    {
        using var connection = _dbService.CreateConnection();
        await connection.OpenAsync();
        
        const string sql = "SELECT * FROM service_rates WHERE is_active = 1 ORDER BY category, service_name";
        var rates = await connection.QueryAsync<ServiceRateEntity>(sql);
        
        return rates.Select(r => new ServiceRateItem
        {
            Id = r.id,
            ServiceName = r.service_name,
            Rate = r.rate,
            Unit = r.unit,
            Category = r.category,
            Glyph = r.glyph,
            IsActive = r.is_active == 1
        });
    }

    public async Task<IEnumerable<ServiceRateItem>> GetAllRatesAsync()
    {
        using var connection = _dbService.CreateConnection();
        await connection.OpenAsync();
        
        const string sql = "SELECT * FROM service_rates ORDER BY category, service_name";
        var rates = await connection.QueryAsync<ServiceRateEntity>(sql);
        
        return rates.Select(r => new ServiceRateItem
        {
            Id = r.id,
            ServiceName = r.service_name,
            Rate = r.rate,
            Unit = r.unit,
            Category = r.category,
            Glyph = r.glyph,
            IsActive = r.is_active == 1
        });
    }

    public async Task AddRateAsync(ServiceRateItem rate)
    {
        using var connection = _dbService.CreateConnection();
        await connection.OpenAsync();
        
        const string sql = @"
            INSERT INTO service_rates (id, service_name, rate, unit, category, glyph, is_active)
            VALUES (@Id, @ServiceName, @Rate, @Unit, @Category, @Glyph, @IsActive)";
            
        await connection.ExecuteAsync(sql, new
        {
            rate.Id,
            rate.ServiceName,
            rate.Rate,
            rate.Unit,
            rate.Category,
            rate.Glyph,
            IsActive = rate.IsActive ? 1 : 0
        });
    }

    public async Task UpdateRateAsync(ServiceRateItem rate)
    {
        using var connection = _dbService.CreateConnection();
        await connection.OpenAsync();
        
        const string sql = @"
            UPDATE service_rates 
            SET service_name = @ServiceName, 
                rate = @Rate, 
                unit = @Unit, 
                category = @Category, 
                glyph = @Glyph, 
                is_active = @IsActive
            WHERE id = @Id";
            
        await connection.ExecuteAsync(sql, new
        {
            rate.Id,
            rate.ServiceName,
            rate.Rate,
            rate.Unit,
            rate.Category,
            rate.Glyph,
            IsActive = rate.IsActive ? 1 : 0
        });
    }

    public async Task DeleteRateAsync(string id)
    {
        using var connection = _dbService.CreateConnection();
        await connection.OpenAsync();
        
        const string sql = "DELETE FROM service_rates WHERE id = @Id";
        await connection.ExecuteAsync(sql, new { Id = id });
    }
    
    // Internal class mapping Dapper query to columns
    private class ServiceRateEntity
    {
        public string id { get; set; } = string.Empty;
        public string service_name { get; set; } = string.Empty;
        public decimal rate { get; set; }
        public string unit { get; set; } = string.Empty;
        public string category { get; set; } = string.Empty;
        public string glyph { get; set; } = string.Empty;
        public int is_active { get; set; }
    }
}
