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
                notes TEXT
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
                customer_id TEXT NOT NULL REFERENCES customers(id),
                session_id TEXT REFERENCES sessions(id),
                amount REAL NOT NULL,
                payment_method TEXT NOT NULL,
                reference_number TEXT,
                payment_date TEXT NOT NULL,
                notes TEXT
            );

            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
        ";

        connection.Execute(sql);
    }
}
