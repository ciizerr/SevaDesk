using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using SevaDesk.Core.Interfaces;
using SevaDesk.Core.Models;

namespace SevaDesk.Infrastructure.Database;

public class CustomTagRepository : ICustomTagRepository
{
    private readonly DatabaseInitializer _db;
    public event EventHandler? CustomTagsChanged;

    public CustomTagRepository(DatabaseInitializer db)
    {
        _db = db;
    }

    private void NotifyChanged() => CustomTagsChanged?.Invoke(this, EventArgs.Empty);

    public async Task<IEnumerable<CustomDocumentTag>> GetAllAsync()
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        const string sql = "SELECT * FROM custom_document_tags ORDER BY created_at ASC";
        var items = await connection.QueryAsync<CustomDocumentTagEntity>(sql);

        return items.Select(i => new CustomDocumentTag
        {
            Id = i.id,
            DisplayName = i.display_name,
            TagKey = i.tag_key,
            CreatedAt = DateTime.TryParse(i.created_at, out var dt) ? dt : DateTime.UtcNow
        });
    }

    public async Task<CustomDocumentTag?> GetByKeyAsync(string tagKey)
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        const string sql = "SELECT * FROM custom_document_tags WHERE LOWER(tag_key) = LOWER(@TagKey) LIMIT 1";
        var entity = await connection.QuerySingleOrDefaultAsync<CustomDocumentTagEntity>(sql, new { TagKey = tagKey.Trim() });
        if (entity == null) return null;

        return new CustomDocumentTag
        {
            Id = entity.id,
            DisplayName = entity.display_name,
            TagKey = entity.tag_key,
            CreatedAt = DateTime.TryParse(entity.created_at, out var dt) ? dt : DateTime.UtcNow
        };
    }

    public async Task<CustomDocumentTag> CreateOrGetAsync(string displayName, string tagKey)
    {
        var cleanKey = tagKey.Trim().ToLowerInvariant();
        var existing = await GetByKeyAsync(cleanKey);
        if (existing != null) return existing;

        var cleanName = string.IsNullOrWhiteSpace(displayName) ? cleanKey : displayName.Trim();
        var item = new CustomDocumentTag
        {
            Id = Guid.NewGuid().ToString(),
            DisplayName = cleanName,
            TagKey = cleanKey,
            CreatedAt = DateTime.UtcNow
        };

        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        const string sql = @"
            INSERT INTO custom_document_tags (id, display_name, tag_key, created_at)
            VALUES (@Id, @DisplayName, @TagKey, @CreatedAt)";

        await connection.ExecuteAsync(sql, new
        {
            item.Id,
            item.DisplayName,
            item.TagKey,
            CreatedAt = item.CreatedAt.ToString("o")
        });

        NotifyChanged();
        return item;
    }

    public async Task<bool> DeleteAsync(string tagKey)
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        const string sql = "DELETE FROM custom_document_tags WHERE LOWER(tag_key) = LOWER(@TagKey)";
        var rows = await connection.ExecuteAsync(sql, new { TagKey = tagKey.Trim() });
        if (rows > 0)
        {
            NotifyChanged();
            return true;
        }
        return false;
    }

    private class CustomDocumentTagEntity
    {
        public string id { get; set; } = string.Empty;
        public string display_name { get; set; } = string.Empty;
        public string tag_key { get; set; } = string.Empty;
        public string created_at { get; set; } = string.Empty;
    }
}
