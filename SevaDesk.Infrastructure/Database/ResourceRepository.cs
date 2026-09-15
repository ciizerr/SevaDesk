using System.Globalization;
using Dapper;
using SevaDesk.Core.Interfaces;
using SevaDesk.Core.Models;

namespace SevaDesk.Infrastructure.Database;

public class ResourceRepository : IResourceRepository
{
    private readonly DatabaseInitializer _db;

    public ResourceRepository(DatabaseInitializer db)
    {
        _db = db;
    }

    public async Task<IEnumerable<ResourceItem>> GetAllAsync()
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        const string sql = @"
            SELECT 
                id AS Id,
                title AS Title,
                category AS Category,
                file_type AS FileType,
                file_size AS FileSize,
                file_path AS FilePath,
                glyph AS Glyph,
                is_favorite AS IsFavorite,
                last_modified AS LastModified
            FROM resources
            ORDER BY is_favorite DESC, title ASC;";

        var rows = await connection.QueryAsync(sql);
        var list = new List<ResourceItem>();

        foreach (var r in rows)
        {
            var item = new ResourceItem
            {
                Id = r.Id,
                Title = r.Title ?? string.Empty,
                Category = r.Category ?? "Forms",
                FileType = r.FileType ?? "PDF",
                FileSize = r.FileSize ?? "1.0 MB",
                FilePath = r.FilePath,
                Glyph = r.Glyph ?? "\uE8A5",
                IsFavorite = r.IsFavorite == 1
            };

            if (DateTime.TryParse((string?)r.LastModified, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            {
                item.LastModified = dt.ToLocalTime();
            }

            list.Add(item);
        }

        return list;
    }

    public async Task ToggleFavoriteAsync(string id, bool isFavorite)
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        const string sql = "UPDATE resources SET is_favorite = @IsFavorite WHERE id = @Id;";
        await connection.ExecuteAsync(sql, new { Id = id, IsFavorite = isFavorite ? 1 : 0 });
    }

    public async Task<ResourceItem> AddCustomResourceAsync(ResourceItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Id))
        {
            item.Id = Guid.NewGuid().ToString();
        }

        item.LastModified = DateTime.UtcNow;

        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        const string sql = @"
            INSERT INTO resources (id, title, category, file_type, file_size, file_path, glyph, is_favorite, last_modified)
            VALUES (@Id, @Title, @Category, @FileType, @FileSize, @FilePath, @Glyph, @IsFavorite, @LastModified);";

        await connection.ExecuteAsync(sql, new
        {
            item.Id,
            item.Title,
            item.Category,
            item.FileType,
            item.FileSize,
            item.FilePath,
            item.Glyph,
            IsFavorite = item.IsFavorite ? 1 : 0,
            LastModified = item.LastModified.ToString("o")
        });

        return item;
    }

    public async Task DeleteAsync(string id)
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        await connection.ExecuteAsync("DELETE FROM resources WHERE id = @Id;", new { Id = id });
    }
}
