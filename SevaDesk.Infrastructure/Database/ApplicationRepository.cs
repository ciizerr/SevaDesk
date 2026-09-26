using System.Globalization;
using Dapper;
using SevaDesk.Core.Interfaces;
using SevaDesk.Core.Models;

namespace SevaDesk.Infrastructure.Database;

public class ApplicationRepository : IApplicationRepository
{
    private readonly DatabaseInitializer _db;

    public event EventHandler? ApplicationsChanged;

    public ApplicationRepository(DatabaseInitializer db)
    {
        _db = db;
    }

    public void NotifyApplicationsChanged() => ApplicationsChanged?.Invoke(this, EventArgs.Empty);


    public async Task<IEnumerable<ApplicationItem>> GetAllAsync(string? status = null)
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        string sql = @"
            SELECT 
                id AS Id,
                customer_id AS CustomerId,
                session_id AS SessionId,
                customer_name AS CustomerName,
                title AS Title,
                portal_name AS PortalName,
                application_number AS ApplicationNumber,
                status AS Status,
                service_charge AS ServiceCharge,
                govt_fee AS GovtFee,
                required_docs AS RequiredDocs,
                notes AS Notes,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM applications";

        object param;
        if (!string.IsNullOrWhiteSpace(status) && status != "All")
        {
            sql += " WHERE status = @Status ORDER BY created_at DESC";
            param = new { Status = status };
        }
        else
        {
            sql += " ORDER BY created_at DESC";
            param = new { };
        }

        var rows = await connection.QueryAsync(sql, param);
        var list = new List<ApplicationItem>();

        foreach (var r in rows)
        {
            var item = new ApplicationItem
            {
                Id = r.Id,
                CustomerId = r.CustomerId,
                SessionId = r.SessionId,
                CustomerName = r.CustomerName ?? string.Empty,
                Title = r.Title ?? string.Empty,
                PortalName = r.PortalName ?? string.Empty,
                ApplicationNumber = r.ApplicationNumber ?? string.Empty,
                Status = r.Status ?? "Draft",
                ServiceCharge = (decimal)(double)r.ServiceCharge,
                GovtFee = (decimal)(double)r.GovtFee,
                RequiredDocs = r.RequiredDocs ?? string.Empty,
                Notes = r.Notes
            };

            if (DateTime.TryParse((string?)r.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dtCreated))
            {
                item.CreatedAt = dtCreated.ToLocalTime();
            }
            if (DateTime.TryParse((string?)r.UpdatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dtUpdated))
            {
                item.UpdatedAt = dtUpdated.ToLocalTime();
            }

            list.Add(item);
        }

        return list;
    }

    public async Task<IEnumerable<ApplicationItem>> GetByCustomerIdAsync(string customerId)
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        const string sql = @"
            SELECT 
                id AS Id,
                customer_id AS CustomerId,
                session_id AS SessionId,
                customer_name AS CustomerName,
                title AS Title,
                portal_name AS PortalName,
                application_number AS ApplicationNumber,
                status AS Status,
                service_charge AS ServiceCharge,
                govt_fee AS GovtFee,
                required_docs AS RequiredDocs,
                notes AS Notes,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM applications
            WHERE customer_id = @CustomerId
            ORDER BY created_at DESC";

        var rows = await connection.QueryAsync(sql, new { CustomerId = customerId });
        var list = new List<ApplicationItem>();

        foreach (var r in rows)
        {
            var item = new ApplicationItem
            {
                Id = r.Id,
                CustomerId = r.CustomerId,
                SessionId = r.SessionId,
                CustomerName = r.CustomerName ?? string.Empty,
                Title = r.Title ?? string.Empty,
                PortalName = r.PortalName ?? string.Empty,
                ApplicationNumber = r.ApplicationNumber ?? string.Empty,
                Status = r.Status ?? "Draft",
                ServiceCharge = (decimal)(double)r.ServiceCharge,
                GovtFee = (decimal)(double)r.GovtFee,
                RequiredDocs = r.RequiredDocs ?? string.Empty,
                Notes = r.Notes
            };

            if (DateTime.TryParse((string?)r.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dtCreated))
            {
                item.CreatedAt = dtCreated.ToLocalTime();
            }
            if (DateTime.TryParse((string?)r.UpdatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dtUpdated))
            {
                item.UpdatedAt = dtUpdated.ToLocalTime();
            }

            list.Add(item);
        }

        return list;
    }

    public async Task<ApplicationItem?> GetByIdAsync(string id)
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        const string sql = "SELECT * FROM applications WHERE id = @Id";
        var r = await connection.QuerySingleOrDefaultAsync(sql, new { Id = id });
        if (r == null) return null;

        var item = new ApplicationItem
        {
            Id = r.id,
            CustomerId = r.customer_id,
            SessionId = r.session_id,
            CustomerName = r.customer_name ?? string.Empty,
            Title = r.title ?? string.Empty,
            PortalName = r.portal_name ?? string.Empty,
            ApplicationNumber = r.application_number ?? string.Empty,
            Status = r.status ?? "Draft",
            ServiceCharge = (decimal)(double)r.service_charge,
            GovtFee = (decimal)(double)r.govt_fee,
            RequiredDocs = r.required_docs ?? string.Empty,
            Notes = r.notes
        };

        if (DateTime.TryParse((string?)r.created_at, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dtCreated))
        {
            item.CreatedAt = dtCreated.ToLocalTime();
        }

        return item;
    }

    public async Task<ApplicationItem> CreateAsync(ApplicationItem app)
    {
        if (string.IsNullOrWhiteSpace(app.Id))
        {
            app.Id = Guid.NewGuid().ToString();
        }

        app.CreatedAt = DateTime.UtcNow;
        app.UpdatedAt = DateTime.UtcNow;

        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        const string sql = @"
            INSERT INTO applications (id, customer_id, session_id, customer_name, title, portal_name, application_number, status, service_charge, govt_fee, required_docs, notes, created_at, updated_at)
            VALUES (@Id, @CustomerId, @SessionId, @CustomerName, @Title, @PortalName, @ApplicationNumber, @Status, @ServiceCharge, @GovtFee, @RequiredDocs, @Notes, @CreatedAt, @UpdatedAt);";

        await connection.ExecuteAsync(sql, new
        {
            app.Id,
            app.CustomerId,
            app.SessionId,
            app.CustomerName,
            app.Title,
            app.PortalName,
            app.ApplicationNumber,
            app.Status,
            app.ServiceCharge,
            app.GovtFee,
            app.RequiredDocs,
            app.Notes,
            CreatedAt = app.CreatedAt.ToString("o"),
            UpdatedAt = app.UpdatedAt.ToString("o")
        });

        NotifyApplicationsChanged();
        return app;
    }


    public async Task UpdateAsync(ApplicationItem app)
    {
        app.UpdatedAt = DateTime.UtcNow;

        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        const string sql = @"
            UPDATE applications
            SET customer_id = @CustomerId,
                session_id = @SessionId,
                customer_name = @CustomerName,
                title = @Title,
                portal_name = @PortalName,
                application_number = @ApplicationNumber,
                status = @Status,
                service_charge = @ServiceCharge,
                govt_fee = @GovtFee,
                required_docs = @RequiredDocs,
                notes = @Notes,
                updated_at = @UpdatedAt
            WHERE id = @Id;";

        await connection.ExecuteAsync(sql, new
        {
            app.Id,
            app.CustomerId,
            app.SessionId,
            app.CustomerName,
            app.Title,
            app.PortalName,
            app.ApplicationNumber,
            app.Status,
            app.ServiceCharge,
            app.GovtFee,
            app.RequiredDocs,
            app.Notes,
            UpdatedAt = app.UpdatedAt.ToString("o")
        });

        NotifyApplicationsChanged();
    }

    public async Task DeleteAsync(string id)
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        await connection.ExecuteAsync("DELETE FROM applications WHERE id = @Id", new { Id = id });
        NotifyApplicationsChanged();
    }


    public async Task<IEnumerable<ApplicationTemplate>> GetAllTemplatesAsync(string? category = null)
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        string sql = @"
            SELECT 
                id AS Id,
                title AS Title,
                category AS Category,
                portal_url AS PortalUrl,
                default_service_fee AS DefaultServiceFee,
                default_govt_fee AS DefaultGovtFee,
                required_docs AS RequiredDocs,
                notes AS Notes,
                is_active AS IsActive,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM application_templates";

        object param;
        if (!string.IsNullOrWhiteSpace(category) && category != "All")
        {
            sql += " WHERE category = @Category ORDER BY title ASC";
            param = new { Category = category };
        }
        else
        {
            sql += " ORDER BY title ASC";
            param = new { };
        }

        var rows = await connection.QueryAsync(sql, param);
        var list = new List<ApplicationTemplate>();

        foreach (var r in rows)
        {
            var item = new ApplicationTemplate
            {
                Id = r.Id,
                Title = r.Title ?? string.Empty,
                Category = r.Category ?? "Jobs & Exams",
                PortalUrl = r.PortalUrl ?? string.Empty,
                DefaultServiceFee = (decimal)(double)r.DefaultServiceFee,
                DefaultGovtFee = (decimal)(double)r.DefaultGovtFee,
                RequiredDocs = r.RequiredDocs ?? string.Empty,
                Notes = r.Notes,
                IsActive = r.IsActive == 1
            };

            if (DateTime.TryParse((string?)r.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dtCreated))
            {
                item.CreatedAt = dtCreated.ToLocalTime();
            }
            if (DateTime.TryParse((string?)r.UpdatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dtUpdated))
            {
                item.UpdatedAt = dtUpdated.ToLocalTime();
            }

            list.Add(item);
        }

        return list;
    }

    public async Task<ApplicationTemplate?> GetTemplateByIdAsync(string id)
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        const string sql = "SELECT * FROM application_templates WHERE id = @Id";
        var r = await connection.QuerySingleOrDefaultAsync(sql, new { Id = id });
        if (r == null) return null;

        var item = new ApplicationTemplate
        {
            Id = r.id,
            Title = r.title ?? string.Empty,
            Category = r.category ?? "Jobs & Exams",
            PortalUrl = r.portal_url ?? string.Empty,
            DefaultServiceFee = (decimal)(double)r.default_service_fee,
            DefaultGovtFee = (decimal)(double)r.default_govt_fee,
            RequiredDocs = r.required_docs ?? string.Empty,
            Notes = r.notes,
            IsActive = r.is_active == 1
        };

        if (DateTime.TryParse((string?)r.created_at, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dtCreated))
        {
            item.CreatedAt = dtCreated.ToLocalTime();
        }
        if (DateTime.TryParse((string?)r.updated_at, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dtUpdated))
        {
            item.UpdatedAt = dtUpdated.ToLocalTime();
        }

        return item;
    }

    public async Task<IEnumerable<ApplicationTemplate>> SearchTemplatesAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await GetAllTemplatesAsync();
        }

        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        const string sql = @"
            SELECT * FROM application_templates
            WHERE title LIKE @Query OR category LIKE @Query OR notes LIKE @Query
            ORDER BY title ASC
            LIMIT 20";

        var rows = await connection.QueryAsync(sql, new { Query = $"%{query}%" });
        var list = new List<ApplicationTemplate>();

        foreach (var r in rows)
        {
            var item = new ApplicationTemplate
            {
                Id = r.id,
                Title = r.title ?? string.Empty,
                Category = r.category ?? "Jobs & Exams",
                PortalUrl = r.portal_url ?? string.Empty,
                DefaultServiceFee = (decimal)(double)r.default_service_fee,
                DefaultGovtFee = (decimal)(double)r.default_govt_fee,
                RequiredDocs = r.required_docs ?? string.Empty,
                Notes = r.notes,
                IsActive = r.is_active == 1
            };

            list.Add(item);
        }

        return list;
    }

    public async Task<ApplicationTemplate> CreateTemplateAsync(ApplicationTemplate template)
    {
        if (string.IsNullOrWhiteSpace(template.Id))
        {
            template.Id = Guid.NewGuid().ToString();
        }

        template.CreatedAt = DateTime.UtcNow;
        template.UpdatedAt = DateTime.UtcNow;

        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        const string sql = @"
            INSERT INTO application_templates (id, title, category, portal_url, default_service_fee, default_govt_fee, required_docs, notes, is_active, created_at, updated_at)
            VALUES (@Id, @Title, @Category, @PortalUrl, @DefaultServiceFee, @DefaultGovtFee, @RequiredDocs, @Notes, @IsActive, @CreatedAt, @UpdatedAt);";

        await connection.ExecuteAsync(sql, new
        {
            template.Id,
            template.Title,
            template.Category,
            template.PortalUrl,
            template.DefaultServiceFee,
            template.DefaultGovtFee,
            template.RequiredDocs,
            template.Notes,
            IsActive = template.IsActive ? 1 : 0,
            CreatedAt = template.CreatedAt.ToString("o"),
            UpdatedAt = template.UpdatedAt.ToString("o")
        });

        return template;
    }

    public async Task UpdateTemplateAsync(ApplicationTemplate template)
    {
        template.UpdatedAt = DateTime.UtcNow;

        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        const string sql = @"
            UPDATE application_templates
            SET title = @Title,
                category = @Category,
                portal_url = @PortalUrl,
                default_service_fee = @DefaultServiceFee,
                default_govt_fee = @DefaultGovtFee,
                required_docs = @RequiredDocs,
                notes = @Notes,
                is_active = @IsActive,
                updated_at = @UpdatedAt
            WHERE id = @Id;";

        await connection.ExecuteAsync(sql, new
        {
            template.Id,
            template.Title,
            template.Category,
            template.PortalUrl,
            template.DefaultServiceFee,
            template.DefaultGovtFee,
            template.RequiredDocs,
            template.Notes,
            IsActive = template.IsActive ? 1 : 0,
            UpdatedAt = template.UpdatedAt.ToString("o")
        });
    }

    public async Task DeleteTemplateAsync(string id)
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        await connection.ExecuteAsync("DELETE FROM application_templates WHERE id = @Id", new { Id = id });
    }
}
