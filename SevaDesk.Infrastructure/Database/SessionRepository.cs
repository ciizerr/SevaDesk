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

            var item = new ActiveSessionItem
            {
                Session = session,
                Customer = customer,
                FolderPath = folderPath,
                FolderStats = stats
            };
            item.UpdateElapsed();
            items.Add(item);
        }

        return items;
    }

    public async Task<ActiveSessionItem> StartSessionAsync(string customerId, string? notes = null)
    {
        var customer = await _customerRepo.GetByIdAsync(customerId)
            ?? throw new InvalidOperationException($"Customer {customerId} not found");

        await _folderManager.SyncToWorkingAsync(customer.Name, customer.Code);

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

        var existing = await connection.QuerySingleOrDefaultAsync<Session>(
            "SELECT id, started_at as StartedAt, duration_seconds as DurationSeconds, status FROM sessions WHERE id = @Id", new { Id = sessionId });

        if (existing != null && existing.Status == "Active")
        {
            var started = existing.StartedAt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(existing.StartedAt, DateTimeKind.Utc)
                : existing.StartedAt;
            var activeSec = Math.Max(0, (int)(DateTime.UtcNow - started).TotalSeconds);
            var totalDuration = existing.DurationSeconds + activeSec;

            await connection.ExecuteAsync(
                "UPDATE sessions SET status = 'Paused', duration_seconds = @Duration WHERE id = @Id",
                new { Id = sessionId, Duration = totalDuration });
        }
        else
        {
            await connection.ExecuteAsync("UPDATE sessions SET status = 'Paused' WHERE id = @Id", new { Id = sessionId });
        }
    }

    public async Task ResumeSessionAsync(string sessionId)
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();
        var now = DateTime.UtcNow.ToString("o");
        await connection.ExecuteAsync(
            "UPDATE sessions SET status = 'Active', started_at = @StartedAt WHERE id = @Id",
            new { Id = sessionId, StartedAt = now });
    }

    public async Task CompleteSessionAsync(string sessionId)
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        var existing = await connection.QuerySingleOrDefaultAsync<Session>(
            "SELECT id, started_at as StartedAt, duration_seconds as DurationSeconds, status FROM sessions WHERE id = @Id", new { Id = sessionId });

        var endedAt = DateTime.UtcNow;
        int durationSec = 0;
        if (existing != null)
        {
            durationSec = existing.DurationSeconds;
            if (existing.Status == "Active")
            {
                var started = existing.StartedAt.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(existing.StartedAt, DateTimeKind.Utc)
                    : existing.StartedAt;
                durationSec += Math.Max(0, (int)(endedAt - started).TotalSeconds);
            }
        }

        await connection.ExecuteAsync(
            "UPDATE sessions SET status = 'Completed', ended_at = @EndedAt, duration_seconds = @Duration WHERE id = @Id",
            new { Id = sessionId, EndedAt = endedAt.ToString("o"), Duration = durationSec }
        );

        if (existing != null)
        {
            var customerIdStr = connection.QuerySingleOrDefault<string>("SELECT customer_id FROM sessions WHERE id = @Id", new { Id = sessionId });
            if (!string.IsNullOrEmpty(customerIdStr))
            {
                var customer = await _customerRepo.GetByIdAsync(customerIdStr);
                if (customer != null)
                {
                    await _folderManager.SyncToBackupAsync(customer.Name, customer.Code);
                }
            }
        }
    }

    public async Task<IEnumerable<Session>> GetCustomerSessionsAsync(string customerId)
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();
        
        const string sql = @"
            SELECT * FROM sessions 
            WHERE customer_id = @CustomerId 
            ORDER BY started_at DESC";

        return await connection.QueryAsync<Session>(sql, new { CustomerId = customerId });
    }
}
