using Microsoft.Data.Sqlite;
using Dapper;

namespace SevaDesk.Infrastructure.Database;

public class DatabaseInitializer
{
    private readonly string _connectionString;

    public DatabaseInitializer(string? customDbPath = null)
    {
        string dbPath;
        if (!string.IsNullOrWhiteSpace(customDbPath))
        {
            dbPath = customDbPath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(appData, "SevaDesk");
            Directory.CreateDirectory(dir);
            dbPath = Path.Combine(dir, "sevadesk.db");
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public string ConnectionString => _connectionString;

    public SqliteConnection CreateConnection() => new(_connectionString);

    public void Initialize()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;

        using var connection = CreateConnection();
        connection.Open();

        const string sql = @"
            PRAGMA journal_mode = WAL;
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS customers (
                id TEXT PRIMARY KEY,
                code TEXT NOT NULL UNIQUE,
                name TEXT NOT NULL,
                mobile TEXT,
                id_type TEXT,
                id_reference TEXT,
                village TEXT,
                notes TEXT,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_customers_code ON customers(code);
            CREATE INDEX IF NOT EXISTS idx_customers_name ON customers(name);
            CREATE INDEX IF NOT EXISTS idx_customers_mobile ON customers(mobile);

            CREATE TABLE IF NOT EXISTS sessions (
                id TEXT PRIMARY KEY,
                customer_id TEXT NOT NULL REFERENCES customers(id),
                started_at TEXT NOT NULL,
                ended_at TEXT,
                status TEXT NOT NULL,
                notes TEXT,
                duration_seconds INTEGER DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS idx_sessions_customer_id ON sessions(customer_id);
            CREATE INDEX IF NOT EXISTS idx_sessions_status ON sessions(status);

            CREATE TABLE IF NOT EXISTS charges (
                id TEXT PRIMARY KEY,
                customer_id TEXT NOT NULL REFERENCES customers(id),
                session_id TEXT REFERENCES sessions(id),
                description TEXT NOT NULL,
                amount REAL NOT NULL,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS payments (
                id TEXT PRIMARY KEY,
                invoice_no TEXT,
                customer_id TEXT,
                customer_name TEXT,
                session_id TEXT,
                amount REAL NOT NULL,
                payment_method TEXT NOT NULL,
                reference_number TEXT,
                payment_date TEXT NOT NULL,
                items_summary TEXT,
                notes TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_payments_date ON payments(payment_date);

            CREATE TABLE IF NOT EXISTS applications (
                id TEXT PRIMARY KEY,
                customer_id TEXT,
                customer_name TEXT,
                title TEXT NOT NULL,
                portal_name TEXT,
                application_number TEXT,
                status TEXT NOT NULL,
                service_charge REAL NOT NULL DEFAULT 0,
                govt_fee REAL NOT NULL DEFAULT 0,
                required_docs TEXT,
                notes TEXT,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_applications_customer_id ON applications(customer_id);
            CREATE INDEX IF NOT EXISTS idx_applications_status ON applications(status);

            CREATE TABLE IF NOT EXISTS resources (
                id TEXT PRIMARY KEY,
                title TEXT NOT NULL,
                category TEXT NOT NULL,
                file_type TEXT NOT NULL,
                file_size TEXT,
                file_path TEXT,
                glyph TEXT NOT NULL,
                is_favorite INTEGER NOT NULL DEFAULT 0,
                last_modified TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS application_templates (
                id TEXT PRIMARY KEY,
                title TEXT NOT NULL,
                category TEXT NOT NULL,
                portal_url TEXT,
                default_service_fee REAL NOT NULL DEFAULT 100,
                default_govt_fee REAL NOT NULL DEFAULT 0,
                required_docs TEXT,
                notes TEXT,
                is_active INTEGER NOT NULL DEFAULT 1,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_app_templates_title ON application_templates(title);
            CREATE INDEX IF NOT EXISTS idx_app_templates_category ON application_templates(category);

            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS service_rates (
                id TEXT PRIMARY KEY,
                service_name TEXT NOT NULL,
                rate REAL NOT NULL,
                unit TEXT NOT NULL,
                category TEXT NOT NULL,
                glyph TEXT NOT NULL,
                is_active INTEGER NOT NULL DEFAULT 1
            );
        ";

        connection.Execute(sql);

        // Safe column migration for payments and sessions if table existed earlier
        try { connection.Execute("ALTER TABLE payments ADD COLUMN invoice_no TEXT;"); } catch { }
        try { connection.Execute("ALTER TABLE payments ADD COLUMN customer_name TEXT;"); } catch { }
        try { connection.Execute("ALTER TABLE payments ADD COLUMN items_summary TEXT;"); } catch { }
        try { connection.Execute("ALTER TABLE sessions ADD COLUMN duration_seconds INTEGER DEFAULT 0;"); } catch { }

        // Ensure default walk-in customer exists for POS walk-in payments
        connection.Execute(@"
            INSERT OR IGNORE INTO customers (id, code, name, mobile, id_type, id_reference, village, notes, created_at, updated_at)
            VALUES ('walk-in', 'CUST-0000', 'Walk-in Customer', '', '', '', '', 'Default walk-in customer for cashier POS', datetime('now'), datetime('now'));
        ");

        // Pre-seed default data if tables are empty
        DatabaseSeeder.SeedAll(connection);
    }


    public string? GetSetting(string key, string? defaultValue = null)
    {
        try
        {
            using var connection = CreateConnection();
            connection.Open();
            var val = connection.QuerySingleOrDefault<string>("SELECT value FROM settings WHERE key = @Key", new { Key = key });
            return val ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    public event Action<string, string>? SettingChanged;

    public void SetSetting(string key, string value)
    {
        try
        {
            using var connection = CreateConnection();
            connection.Open();
            connection.Execute(@"
                INSERT INTO settings (key, value) VALUES (@Key, @Value)
                ON CONFLICT(key) DO UPDATE SET value = @Value",
                new { Key = key, Value = value });

            SettingChanged?.Invoke(key, value);
        }
        catch { }
    }
}
