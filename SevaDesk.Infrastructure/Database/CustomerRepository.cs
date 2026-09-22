using Dapper;
using SevaDesk.Core.Interfaces;
using SevaDesk.Core.Models;

namespace SevaDesk.Infrastructure.Database;

public class CustomerRepository : ICustomerRepository
{
    private readonly DatabaseInitializer _db;

    public CustomerRepository(DatabaseInitializer db)
    {
        _db = db;
    }

    public async Task<string> GenerateNextCodeAsync()
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        var codes = await connection.QueryAsync<string>("SELECT code FROM customers WHERE code LIKE 'CUST-%'");
        int maxNum = 0;

        foreach (var code in codes)
        {
            if (code.StartsWith("CUST-") && int.TryParse(code[5..], out var n) && n > maxNum)
            {
                maxNum = n;
            }
        }

        return $"CUST-{(maxNum + 1):D4}";
    }

    public async Task<Customer?> GetByIdAsync(string id)
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        const string sql = "SELECT * FROM customers WHERE id = @Id";
        return await connection.QuerySingleOrDefaultAsync<Customer>(sql, new { Id = id });
    }

    public async Task<IEnumerable<Customer>> SearchAsync(string query)
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        if (string.IsNullOrWhiteSpace(query))
        {
            const string allSql = "SELECT * FROM customers ORDER BY created_at DESC LIMIT 50";
            return await connection.QueryAsync<Customer>(allSql);
        }

        const string sql = @"
            SELECT * FROM customers
            WHERE name LIKE @Pattern
               OR mobile LIKE @Pattern
               OR code LIKE @Pattern
               OR village LIKE @Pattern
               OR id_reference LIKE @Pattern
            ORDER BY created_at DESC
            LIMIT 50";

        return await connection.QueryAsync<Customer>(sql, new { Pattern = $"%{query.Trim()}%" });
    }

    public async Task<Customer> CreateAsync(Customer customer)
    {
        if (string.IsNullOrWhiteSpace(customer.Code))
        {
            customer.Code = await GenerateNextCodeAsync();
        }

        customer.CreatedAt = DateTime.UtcNow;
        customer.UpdatedAt = DateTime.UtcNow;

        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        const string sql = @"
            INSERT INTO customers (id, code, name, mobile, id_type, id_reference, village, notes, created_at, updated_at)
            VALUES (@Id, @Code, @Name, @Mobile, @IdType, @IdReference, @Village, @Notes, @CreatedAt, @UpdatedAt)";

        await connection.ExecuteAsync(sql, new
        {
            customer.Id,
            customer.Code,
            customer.Name,
            customer.Mobile,
            customer.IdType,
            customer.IdReference,
            customer.Village,
            customer.Notes,
            CreatedAt = customer.CreatedAt.ToString("o"),
            UpdatedAt = customer.UpdatedAt.ToString("o")
        });

        return customer;
    }

    public async Task UpdateAsync(Customer customer)
    {
        customer.UpdatedAt = DateTime.UtcNow;

        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        const string sql = @"
            UPDATE customers
            SET name = @Name,
                mobile = @Mobile,
                id_type = @IdType,
                id_reference = @IdReference,
                village = @Village,
                notes = @Notes,
                updated_at = @UpdatedAt
            WHERE id = @Id";

        await connection.ExecuteAsync(sql, new
        {
            customer.Id,
            customer.Name,
            customer.Mobile,
            customer.IdType,
            customer.IdReference,
            customer.Village,
            customer.Notes,
            UpdatedAt = customer.UpdatedAt.ToString("o")
        });
    }
}

