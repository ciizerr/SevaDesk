using SevaDesk.Core.Models;

namespace SevaDesk.Core.Interfaces;

public interface ISessionRepository
{
    Task<IEnumerable<ActiveSessionItem>> GetActiveSessionsAsync();
    Task<ActiveSessionItem> StartSessionAsync(string customerId, string? notes = null);
    Task PauseSessionAsync(string sessionId);
    Task ResumeSessionAsync(string sessionId);
    Task CompleteSessionAsync(string sessionId);
}
