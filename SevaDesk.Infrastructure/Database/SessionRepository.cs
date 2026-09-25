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

    public async Task PauseSessionAsync(string sessionId, int? knownDurationSeconds = null)
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        if (knownDurationSeconds.HasValue)
        {
            await connection.ExecuteAsync(
                "UPDATE sessions SET status = 'Paused', duration_seconds = @Duration WHERE id = @Id",
                new { Id = sessionId, Duration = knownDurationSeconds.Value });
            return;
        }

        var row = await connection.QuerySingleOrDefaultAsync<(string? id, string? started_at, int? duration_seconds, string? status)>(
            "SELECT id, started_at, duration_seconds, status FROM sessions WHERE id = @Id", new { Id = sessionId });

        if (row != default && row.status == "Active")
        {
            DateTime startedUtc = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(row.started_at) && DateTime.TryParse(row.started_at, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedDt))
            {
                startedUtc = parsedDt.Kind == DateTimeKind.Utc 
                    ? parsedDt 
                    : (parsedDt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(parsedDt, DateTimeKind.Utc) : parsedDt.ToUniversalTime());
            }

            int existingDuration = row.duration_seconds ?? 0;
            var activeSec = Math.Max(0, (int)(DateTime.UtcNow - startedUtc).TotalSeconds);
            var totalDuration = existingDuration + activeSec;

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

        // Include customer_id in initial SELECT to avoid a second round-trip
        var existing = await connection.QuerySingleOrDefaultAsync<Session>(
            @"SELECT id, customer_id as CustomerId, started_at as StartedAt,
                     duration_seconds as DurationSeconds, status
              FROM sessions WHERE id = @Id",
            new { Id = sessionId });

        var endedAt = DateTime.UtcNow;
        int durationSec = 0;
        if (existing != null)
        {
            durationSec = existing.DurationSeconds;
            if (existing.Status == "Active")
            {
                var startedUtc = existing.StartedAt.Kind == DateTimeKind.Utc
                    ? existing.StartedAt
                    : (existing.StartedAt.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(existing.StartedAt, DateTimeKind.Utc)
                        : existing.StartedAt.ToUniversalTime());
                durationSec += Math.Max(0, (int)(endedAt - startedUtc).TotalSeconds);
            }
        }

        await connection.ExecuteAsync(
            "UPDATE sessions SET status = 'Completed', ended_at = @EndedAt, duration_seconds = @Duration WHERE id = @Id",
            new { Id = sessionId, EndedAt = endedAt.ToString("o"), Duration = durationSec }
        );

        // Sync to backup and record the working folder path + sync timestamp
        if (existing != null && !string.IsNullOrEmpty(existing.CustomerId))
        {
            var customer = await _customerRepo.GetByIdAsync(existing.CustomerId);
            if (customer != null)
            {
                var workingFolder = _folderManager.GetCustomerFolderPath(customer.Name, customer.Code);
                bool syncSuccess = false;
                try
                {
                    await _folderManager.SyncToBackupAsync(customer.Name, customer.Code);
                    syncSuccess = true;
                }
                catch { /* swallow — sync is best-effort */ }

                if (syncSuccess)
                {
                    var syncedAt = DateTime.UtcNow.ToString("o");
                    // Record path + sync timestamp so startup cleanup can prune old working folders
                    await connection.ExecuteAsync(
                        @"UPDATE sessions
                          SET working_folder_path = @FolderPath,
                              backup_synced_at    = @SyncedAt
                          WHERE id = @Id",
                        new { Id = sessionId, FolderPath = workingFolder, SyncedAt = syncedAt }
                    );
                }
                else if (!string.IsNullOrEmpty(_folderManager.BackupDirectory))
                {
                    // Backup directory is set but sync failed — warn on next startup
                    // Still record the working folder path so admin can retry manually
                    await connection.ExecuteAsync(
                        "UPDATE sessions SET working_folder_path = @FolderPath WHERE id = @Id",
                        new { Id = sessionId, FolderPath = workingFolder }
                    );
                }
            }
        }
    }

    public async Task DeleteSessionAsync(string sessionId)
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        // 1. Unlink any payments referencing this session
        await connection.ExecuteAsync(
            "UPDATE payments SET session_id = NULL WHERE session_id = @Id",
            new { Id = sessionId }
        );

        // 2. Remove charges linked to this session
        await connection.ExecuteAsync(
            "DELETE FROM charges WHERE session_id = @Id",
            new { Id = sessionId }
        );

        // 3. Delete the session
        await connection.ExecuteAsync(
            "DELETE FROM sessions WHERE id = @Id",
            new { Id = sessionId }
        );
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
