using SevaDesk.Core.Interfaces;
using SevaDesk.Infrastructure.Database;
using SevaDesk.Infrastructure.FileManager;

namespace SevaDesk_App.Services;

public static class AppServices
{
    public static DatabaseInitializer Database { get; } = new();
    public static IFolderManager FolderManager { get; } = new FolderManager();
    public static ICustomerRepository Customers { get; } = new CustomerRepository(Database);
    public static ISessionRepository Sessions { get; } = new SessionRepository(Database, Customers, FolderManager);
    public static LocalizationService Localization { get; } = new();

    public static void Initialize()
    {
        Database.Initialize();
        Localization.Initialize();
    }
}
