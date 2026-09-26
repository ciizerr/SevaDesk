using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SevaDesk.Core.Models;

namespace SevaDesk.Core.Interfaces;

public interface ICustomTagRepository
{
    event EventHandler? CustomTagsChanged;
    Task<IEnumerable<CustomDocumentTag>> GetAllAsync();
    Task<CustomDocumentTag?> GetByKeyAsync(string tagKey);
    Task<CustomDocumentTag> CreateOrGetAsync(string displayName, string tagKey);
    Task<bool> DeleteAsync(string tagKey);
}
