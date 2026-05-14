using Microsoft.Data.Sqlite;

namespace FactoryGame.Infrastructure.Data;

/// <summary>
/// Keeps one open SQLite connection so a shared in-memory database
/// (<c>Mode=Memory</c> + <c>Cache=Shared</c>) survives between startup and HTTP requests.
/// </summary>
internal sealed class SqliteSharedMemoryDatabasePin : IDisposable
{
    public SqliteSharedMemoryDatabasePin(string connectionString)
    {
        Connection = new SqliteConnection(connectionString);
        Connection.Open();
    }

    public SqliteConnection Connection { get; }

    public void Dispose() => Connection.Dispose();
}
