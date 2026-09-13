using Dapper;
using SevaDesk.Core.Interfaces;
using SevaDesk.Core.Models;

namespace SevaDesk.Infrastructure.Database;

public class SessionRepository : ISessionRepository
{
    private readonly DatabaseInitializer _db;
    private readonly ICustomerRepository _customerRepo;
    private readonly IFolderManager _folderManager;

    public SessionRepository(DatabaseInitializer db, ICustomerRepository customerRepo, IFolderManager folderManager)
    {
        _db = db;
        _customerRepo = customerRepo;
        _folderManager = folderManager;
    }

    public async Task<IEnumerable<ActiveSessionItem>> GetActiveSessionsAsync()
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        const string sql = @"
            SELECT s.*, c.*
            FROM sessions s
            JOIN customers c ON s.customer_id = c.id
            WHERE s.status IN ('Active', 'Paused')
            ORDER BY s.started_at DESC";

        var items = new List<ActiveSessionItem>();

        var rows = await connection.QueryAsync<Session, Customer, (Session Session, Customer Customer)>(
            sql,
            (s, c) => (s, c),
            splitOn: "id"
        );

        foreach (var (session, customer) in rows)
        {
            var folderPath = _folderManager.GetCustomerFolderPath(customer.Name, customer.Code);
            var stats = _folderManager.GetFolderStats(folderPath);

            items.Add(new ActiveSessionItem
            {
                Session = session,
                Customer = customer,
                FolderPath = folderPath,
                FolderStats = stats
            });
        }

        return items;
    }

    public async Task<ActiveSessionItem> StartSessionAsync(string customerId, string? notes = null)
    {
        var customer = await _customerRepo.GetByIdAsync(customerId)
            ?? throw new InvalidOperationException($"Customer {customerId} not found");

        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        // Check if an existing active or paused session exists
        const string checkSql = "SELECT * FROM sessions WHERE customer_id = @CustomerId AND status IN ('Active', 'Paused') LIMIT 1";
        var existing = await connection.QuerySingleOrDefaultAsync<Session>(checkSql, new { CustomerId = customerId });

        if (existing != null)
        {
            if (existing.Status == "Paused")
            {
                await ResumeSessionAsync(existing.Id);
                existing.Status = "Active";
            }

            var fPath = _folderManager.EnsureCustomerWorkingFolder(customer.Name, customer.Code);
            return new ActiveSessionItem
            {
                Session = existing,
                Customer = customer,
                FolderPath = fPath,
                FolderStats = _folderManager.GetFolderStats(fPath)
            };
        }

        var newSession = new Session
        {
            Id = Guid.NewGuid().ToString(),
            CustomerId = customer.Id,
            StartedAt = DateTime.UtcNow,
            Status = "Active",
            Notes = notes
        };

        const string insertSql = @"
            INSERT INTO sessions (id, customer_id, started_at, status, notes)
            VALUES (@Id, @CustomerId, @StartedAt, @Status, @Notes)";

        await connection.ExecuteAsync(insertSql, new
        {
            newSession.Id,
            newSession.CustomerId,
            StartedAt = newSession.StartedAt.ToString("o"),
            newSession.Status,
            newSession.Notes
        });

        var folderPath = _folderManager.EnsureCustomerWorkingFolder(customer.Name, customer.Code);

        return new ActiveSessionItem
        {
            Session = newSession,
            Customer = customer,
            FolderPath = folderPath,
            FolderStats = _folderManager.GetFolderStats(folderPath)
        };
    }

    public async Task PauseSessionAsync(string sessionId)
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();
        await connection.ExecuteAsync("UPDATE sessions SET status = 'Paused' WHERE id = @Id", new { Id = sessionId });
    }

    public async Task ResumeSessionAsync(string sessionId)
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();
        await connection.ExecuteAsync("UPDATE sessions SET status = 'Active' WHERE id = @Id", new { Id = sessionId });
    }

    public async Task CompleteSessionAsync(string sessionId)
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            "UPDATE sessions SET status = 'Completed', ended_at = @EndedAt WHERE id = @Id",
            new { Id = sessionId, EndedAt = DateTime.UtcNow.ToString("o") }
        );
    }
}
